using Terraria;

namespace NoxusBoss.Core.Graphics.Players;

/// <summary>
/// A representation of a post-processing effect that can be applied to a given player.
/// </summary>
/// 
/// <remarks>
/// Instances of these should be cached where possible.
/// </remarks>
/// <param name="Effect">The action that handles post-processing.</param>
/// <param name="ClearOnWorldUpdateCycle">Whether this effect should be automatically cleared on the world update, instead of render update cycle.</param>
public record PlayerPostProcessingEffect(Action<Player> Effect, bool ClearOnWorldUpdateCycle);
