// <copyright file="ServerShutdown.cs" company="Steve Guidetti">
// Copyright (c) Steve Guidetti. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace SevenMod.Plugin.ServerShutdown
{
    using System;
    using System.Collections.Generic;
    using System.Timers;
    using SevenMod.Chat;
    using SevenMod.ConVar;
    using SevenMod.Core;

    /// <summary>
    /// Plugin that schedules automatic server shutdowns and enables shutdown votes.
    /// </summary>
    public sealed class ServerShutdown : PluginAbstract, IDisposable
    {
        /// <summary>
        /// The value of the ServerShutdownSchedule <see cref="ConVar"/>.
        /// </summary>
        private ConVarValue schedule;

        /// <summary>
        /// The value of the ServerShutdownEnableVote <see cref="ConVar"/>.
        /// </summary>
        private ConVarValue enableVote;

        /// <summary>
        /// The list of automatic shutdown times as the number of minutes since midnight.
        /// </summary>
        private List<int> shutdownSchedule = new List<int>();

        /// <summary>
        /// The timer for the next shutdown event.
        /// </summary>
        private Timer shutdownTimer;

        /// <summary>
        /// The current minute of the 5 minute countdown.
        /// </summary>
        private int countdown;

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
            this.schedule = this.CreateConVar("ServerShutdownSchedule", string.Empty, "The automatic shutdown schedule in the format HH:MM. Separate multiple times with commas.").Value;
            this.enableVote = this.CreateConVar("ServerShutdownEnableVote", "True", "Enable the voteshutdown admin command.").Value;

            this.AutoExecConfig(true, "ServerShutdown");

            this.schedule.ConVar.ValueChanged += this.OnScheduleChanged;
            this.enableVote.ConVar.ValueChanged += this.OnEnableVoteChanged;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ((IDisposable)this.shutdownTimer).Dispose();
        }

        /// <summary>
        /// Called when the value of the ServerShutdownSchedule <see cref="ConVar"/> is changed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A <see cref="ConVarChangedEventArgs"/> object containing the event data.</param>
        private void OnScheduleChanged(object sender, ConVarChangedEventArgs e)
        {
            if (this.shutdownTimer != null)
            {
                this.shutdownTimer.Dispose();
                this.shutdownTimer = null;
            }

            this.shutdownSchedule.Clear();
            foreach (var time in this.schedule.AsString.Split(','))
            {
                if (string.IsNullOrEmpty(time))
                {
                    continue;
                }

                var index = time.IndexOf(':');
                if (index > 0 && int.TryParse(time.Substring(0, index), out var hour) && int.TryParse(time.Substring(index + 1), out var minute))
                {
                    this.shutdownSchedule.Add((hour * 60) + minute);
                    continue;
                }

                this.LogError($"Invalid schedule time: {time}");
            }

            if (this.shutdownSchedule.Count > 0)
            {
                this.shutdownSchedule.Sort();
                this.ScheduleNext();
            }
        }

        /// <summary>
        /// Called when the value of the ServerShutdownEnableVote <see cref="ConVar"/> is changed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A <see cref="ConVarChangedEventArgs"/> object containing the event data.</param>
        private void OnEnableVoteChanged(object sender, ConVarChangedEventArgs e)
        {
        }

        /// <summary>
        /// Schedules the next automatic shutdown.
        /// </summary>
        private void ScheduleNext()
        {
            if (this.shutdownTimer != null)
            {
                this.shutdownTimer.Dispose();
            }

            this.countdown = 5;

            var time = this.shutdownSchedule.Find((int t) => (t - 5) >= DateTime.Now.TimeOfDay.TotalMinutes);
            time = time == 0 ? this.shutdownSchedule[0] : time;

            var dt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, time / 60, time % 60, 0);
            if (dt < DateTime.Now)
            {
                dt = dt.AddDays(1);
            }

            this.shutdownTimer = new Timer(dt.AddMinutes(-5).Subtract(DateTime.Now).TotalMilliseconds);
            this.shutdownTimer.Elapsed += this.OnShutdownTimerElapsed;
            this.shutdownTimer.Enabled = true;
        }

        /// <summary>
        /// Called by the <see cref="shutdownTimer"/> to handle the next shutdown event.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="ElapsedEventArgs"/> object containing the event data.</param>
        private void OnShutdownTimerElapsed(object sender, ElapsedEventArgs e)
        {
            this.shutdownTimer.Dispose();
            this.shutdownTimer = null;
            if (this.countdown > 0)
            {
                this.shutdownTimer = new Timer(60000);
                this.shutdownTimer.Elapsed += this.OnShutdownTimerElapsed;
                this.shutdownTimer.Enabled = true;

                ChatHelper.SendToAll($"[FF0000]Warning: Server shutting down in [b]{this.countdown} minutes[/b][-]");

                this.countdown--;
            }
            else
            {
                SdtdConsole.Instance.ExecuteSync("shutdown", null);
                this.ScheduleNext();
            }
        }
    }
}
