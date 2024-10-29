// <copyright file="Mod.cs" company="Yenyang's Mods. MIT License">
// Copyright (c) Yenyang's Mods. MIT License. All rights reserved.
// </copyright>
namespace BrokenSegmentDetector
{
    using BrokenSegmentDetector.Systems;
    using Colossal.Logging;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;

    /// <summary>
    /// Mod entry point. Initializes system.
    /// </summary>
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(BrokenSegmentDetector)}").SetShowsErrorsInUI(false);

        /// <inheritdoc/>
        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                log.Info($"Current mod asset at {asset.path}");
            }

            updateSystem.UpdateAt<FindBrokenNodesSystem>(SystemUpdatePhase.GameSimulation);
        }

        /// <inheritdoc/>
        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
        }
    }
}
