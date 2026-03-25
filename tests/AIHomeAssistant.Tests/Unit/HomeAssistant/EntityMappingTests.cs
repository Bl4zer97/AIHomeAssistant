using AIHomeAssistant.Core.Models;

namespace AIHomeAssistant.Tests.Unit.HomeAssistant;

public class EntityMappingTests
{
    private static EntityMappingOptions MakeOptions() => new()
    {
        Lights = new Dictionary<string, string>
        {
            ["salotto"] = "light.living_room",
            ["cucina"] = "light.kitchen"
        },
        Climate = new Dictionary<string, string>
        {
            ["salotto"] = "climate.living_room"
        }
    };

    [Fact]
    public void EntityMapping_TryResolveEntityId_WhenLightFriendlyNameExists_ReturnsEntityId()
    {
        // Arrange
        var options = MakeOptions();

        // Act
        var resolved = EntityMapping.TryResolveEntityId(options, "lights", "salotto", out var entityId);

        // Assert
        Assert.True(resolved);
        Assert.Equal("light.living_room", entityId);
    }

    [Fact]
    public void EntityMapping_TryResolveEntityId_WhenClimateFriendlyNameExists_ReturnsEntityId()
    {
        // Arrange
        var options = MakeOptions();

        // Act
        var resolved = EntityMapping.TryResolveEntityId(options, "climate", "salotto", out var entityId);

        // Assert
        Assert.True(resolved);
        Assert.Equal("climate.living_room", entityId);
    }

    [Fact]
    public void EntityMapping_TryResolveEntityId_WhenFriendlyNameMissing_ReturnsFalse()
    {
        // Arrange
        var options = MakeOptions();

        // Act
        var resolved = EntityMapping.TryResolveEntityId(options, "lights", "bagno", out var entityId);

        // Assert
        Assert.False(resolved);
        Assert.True(string.IsNullOrEmpty(entityId));
    }

    [Fact]
    public void EntityMapping_TryResolveEntityId_WhenDomainUnknown_ReturnsFalse()
    {
        // Arrange
        var options = MakeOptions();

        // Act
        var resolved = EntityMapping.TryResolveEntityId(options, "sensors", "salotto", out var entityId);

        // Assert
        Assert.False(resolved);
        Assert.Equal(string.Empty, entityId);
    }

    [Fact]
    public void EntityMapping_TryResolveEntityId_WhenLightKitchenExists_ReturnsCorrectEntityId()
    {
        // Arrange
        var options = MakeOptions();

        // Act
        var resolved = EntityMapping.TryResolveEntityId(options, "lights", "cucina", out var entityId);

        // Assert
        Assert.True(resolved);
        Assert.Equal("light.kitchen", entityId);
    }
}
