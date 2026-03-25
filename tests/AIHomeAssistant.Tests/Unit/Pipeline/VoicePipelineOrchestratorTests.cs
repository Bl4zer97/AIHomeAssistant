using System.Threading.Channels;
using AIHomeAssistant.Core.Interfaces;
using AIHomeAssistant.Core.Models;
using AIHomeAssistant.Infrastructure.Options;
using AIHomeAssistant.Infrastructure.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AIHomeAssistant.Tests.Unit.Pipeline;

public class VoicePipelineOrchestratorTests
{
    private readonly ISpeechToTextService _stt = Substitute.For<ISpeechToTextService>();
    private readonly IIntentResolutionService _intent = Substitute.For<IIntentResolutionService>();
    private readonly ICommandRouter _router = Substitute.For<ICommandRouter>();
    private readonly IHaStateCacheService _haCache = Substitute.For<IHaStateCacheService>();
    private readonly IAudioFeedbackService _audio = Substitute.For<IAudioFeedbackService>();
    private readonly ICommandRepository _log = Substitute.For<ICommandRepository>();
    private readonly ILogger<VoicePipelineOrchestrator> _logger =
        Substitute.For<ILogger<VoicePipelineOrchestrator>>();

    private VoicePipelineOrchestrator CreateSut(Channel<AudioSegment> channel)
    {
        // Set up IServiceScopeFactory to return ICommandRouter and ICommandRepository mocks
        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(ICommandRouter)).Returns(_router);
        sp.GetService(typeof(ICommandRepository)).Returns(_log);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(sp);

        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);

        var opts = Options.Create(new AudioOptions { FeedbackProfile = "tone" });
        return new VoicePipelineOrchestrator(
            channel, _stt, _intent, _haCache, _audio, factory, opts, _logger);
    }

    private static AudioSegment MakeSegment() => new(new short[512]);

    [Fact]
    public async Task Pipeline_WhenSttFails_PlaysErrorAndLogsWithErrorCode()
    {
        var channel = Channel.CreateUnbounded<AudioSegment>();
        var sut = CreateSut(channel);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _stt.TranscribeAsync(Arg.Any<AudioSegment>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<string>(false,
                Error: new PipelineError("STT_LOW_CONFIDENCE", "Low confidence")));

        await sut.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(MakeSegment(), cts.Token);
        await Task.Delay(200, cts.Token);
        await sut.StopAsync(CancellationToken.None);

        await _audio.Received(1).PlayErrorAsync(Arg.Any<CancellationToken>());
        await _log.Received(1).InsertAsync(
            Arg.Is<CommandRecord>(r => r.ErrorCode == "STT_LOW_CONFIDENCE"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pipeline_WhenIntentFails_PlaysErrorAndLogsWithErrorCode()
    {
        var channel = Channel.CreateUnbounded<AudioSegment>();
        var sut = CreateSut(channel);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _stt.TranscribeAsync(Arg.Any<AudioSegment>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<string>(true, "accendi la luce"));

        _haCache.GetAllStates().Returns((IReadOnlyList<HaState>)new List<HaState>());

        _intent.ResolveIntentAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<HaState>>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<IntentResult>(false,
                Error: new PipelineError("INTENT_RESOLUTION_FAILED", "LLM error")));

        await sut.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(MakeSegment(), cts.Token);
        await Task.Delay(200, cts.Token);
        await sut.StopAsync(CancellationToken.None);

        await _audio.Received(1).PlayErrorAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pipeline_WhenSuccess_PlaysSuccessAndLogsHaResponse200()
    {
        var channel = Channel.CreateUnbounded<AudioSegment>();
        var sut = CreateSut(channel);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _stt.TranscribeAsync(Arg.Any<AudioSegment>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<string>(true, "accendi la luce del salotto"));

        _haCache.GetAllStates().Returns((IReadOnlyList<HaState>)new List<HaState>());

        var intentResult = new IntentResult(
            IntentType.Action, "light.living_room", "light.turn_on",
            new Dictionary<string, object?>());
        _intent.ResolveIntentAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<HaState>>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<IntentResult>(true, intentResult));

        _router.RouteAsync(Arg.Any<IntentResult>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<string?>(true, null));

        await sut.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(MakeSegment(), cts.Token);
        await Task.Delay(200, cts.Token);
        await sut.StopAsync(CancellationToken.None);

        await _audio.Received(1).PlaySuccessAsync(Arg.Any<CancellationToken>());
        await _log.Received(1).InsertAsync(
            Arg.Is<CommandRecord>(r => r.HaResponseCode == 200 && r.ErrorCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pipeline_WhenQueryIntent_PlaysSpeechWithResponse()
    {
        var channel = Channel.CreateUnbounded<AudioSegment>();
        var sut = CreateSut(channel);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _stt.TranscribeAsync(Arg.Any<AudioSegment>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<string>(true, "quante luci sono accese?"));

        _haCache.GetAllStates().Returns((IReadOnlyList<HaState>)new List<HaState>());

        var queryIntent = new IntentResult(
            IntentType.Query, "light.living_room", "light.get_state",
            new Dictionary<string, object?>());
        _intent.ResolveIntentAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<HaState>>(),
                Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<IntentResult>(true, queryIntent));

        _router.RouteAsync(Arg.Any<IntentResult>(), Arg.Any<CancellationToken>())
            .Returns(new PipelineResult<string?>(true, "La luce del salotto e accesa"));

        await sut.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(MakeSegment(), cts.Token);
        await Task.Delay(200, cts.Token);
        await sut.StopAsync(CancellationToken.None);

        await _audio.Received(1).PlaySpeechAsync(
            "La luce del salotto e accesa", Arg.Any<CancellationToken>());
    }
}