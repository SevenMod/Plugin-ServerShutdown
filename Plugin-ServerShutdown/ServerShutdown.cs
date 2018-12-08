// <copyright file="ServerShutdown.cs" company="Steve Guidetti">
// Copyright (c) Steve Guidetti. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace SevenMod.Plugin.ServerShutdown
{
    using SevenMod.Core;

    /// <summary>
    /// Plugin that schedules automatic server shutdowns and enables shutdown votes.
    /// </summary>
    public sealed class ServerShutdown : PluginAbstract
    {
        /// <inheritdoc/>
        public override PluginInfo Info => new PluginInfo
        {
            Name = "ServerShutdown",
            Author = "SevenMod",
            Description = "Schedules automatic server shutdowns and enables shutdown votes.",
            Version = "0.1.0.0",
            Website = "https://github.com/SevenMod/Plugin-ServerShutdown"
        };

        /// <inheritdoc/>
        public override void OnLoadPlugin()
        {
        }
    }
}
