﻿// <copyright file="ServerShutdown.cs" company="Steve Guidetti">
// Copyright (c) Steve Guidetti. All rights reserved.
// Licensed under the MIT license. See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace SevenMod.Plugin.ServerShutdown
{
    using System;
    using System.Collections.Generic;
    using System.Timers;
    using SevenMod.Chat;
    using SevenMod.Console;
    using SevenMod.ConVar;
    using SevenMod.Core;
    using SevenMod.Voting;

    /// <summary>
    /// Plugin that schedules automatic server shutdowns and enables shutdown votes.
    /// </summary>
    public sealed class ServerShutdown : PluginAbstract, IDisposable
    {
        /// <summary>
        /// The value of the ServerShutdownAutoRestart <see cref="ConVar"/>.
        /// </summary>
        private ConVarValue autoRestart;

        /// <summary>
        /// The value of the ServerShutdownSchedule <see cref="ConVar"/>.
        /// </summary>
        private ConVarValue schedule;

        /// <summary>
        /// The value of the ServerShutdownEnableVote <see cref="ConVar"/>.
        /// </summary>
        private ConVarValue enableVote;

        /// <summary>
        /// The value of the ServerShutdownVotePercent <see cref="ConVar"/>.
        /// </summary>
        private ConVarValue votePercent;

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

        /// <summary>
        /// A value indicating whether a shutdown is in progress.
        /// </summary>
        private bool shutdownInProgress;

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
            this.autoRestart = this.CreateConVar("ServerShutdownAutoRestart", "True", "Enale if the server is set up to automatically restart after crashing.").Value;
            this.schedule = this.CreateConVar("ServerShutdownSchedule", string.Empty, "The automatic shutdown schedule in the format HH:MM. Separate multiple times with commas.").Value;
            this.enableVote = this.CreateConVar("ServerShutdownEnableVote", "True", "Enable the voteshutdown admin command.").Value;
            this.votePercent = this.CreateConVar("ServerShutdownVotePercent", "0.60", "The percentage of players that must vote yes for a successful shutdown vote.", true, 0, true, 1).Value;

            this.AutoExecConfig(true, "ServerShutdown");

            this.RegisterCommands();

            this.autoRestart.ConVar.ValueChanged += this.OnAutoRestartChanged;
            this.schedule.ConVar.ValueChanged += this.OnScheduleChanged;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ((IDisposable)this.shutdownTimer).Dispose();
        }

        /// <summary>
        /// Registers the admin commands.
        /// </summary>
        private void RegisterCommands()
        {
            if (this.autoRestart.AsBool)
            {
                this.RegAdminCmd("voterestart", Admin.AdminFlags.Vote, "Starts a vote to restart the server").Executed += this.OnVoteShutdownExecuted;
                this.RegAdminCmd("cancelrestart", Admin.AdminFlags.Changemap, "Cancels an impending restart").Executed += this.OnCancelShutdownExecuted;
            }
            else
            {
                this.RegAdminCmd("voteshutdown", Admin.AdminFlags.Vote, "Starts a vote to shut down the server").Executed += this.OnVoteShutdownExecuted;
                this.RegAdminCmd("cancelshutdown", Admin.AdminFlags.Changemap, "Cancels an impending shutdown").Executed += this.OnCancelShutdownExecuted;
            }
        }

        /// <summary>
        /// Called when the value of the ServerShutdownAutoRestart <see cref="ConVar"/> is changed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A <see cref="ConVarChangedEventArgs"/> object containing the event data.</param>
        private void OnAutoRestartChanged(object sender, ConVarChangedEventArgs e)
        {
            this.UnregAdminCommand("voteshutdown");
            this.UnregAdminCommand("voterestart");

            this.RegisterCommands();
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
                if (this.shutdownSchedule.Count > 1)
                {
                    this.shutdownSchedule.Sort();
                    for (var i = 0; i < this.shutdownSchedule.Count; i++)
                    {
                        int diff;
                        if (i == 0)
                        {
                            diff = (1440 - this.shutdownSchedule[this.shutdownSchedule.Count - 1]) + this.shutdownSchedule[0];
                        }
                        else
                        {
                            diff = this.shutdownSchedule[i] - this.shutdownSchedule[i - 1];
                        }

                        if (diff <= 5)
                        {
                            this.LogError($"Removing scheduled time {this.shutdownSchedule[i] / 60:D2}:{this.shutdownSchedule[i] % 60:D2} because it is not more than 5 minutes after the previous time");
                            this.shutdownSchedule.RemoveAt(i);
                            i--;
                        }
                    }
                }

                this.ScheduleNext();
            }
        }

        /// <summary>
        /// Called when the voteshutdown admin command is executed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="AdminCommandEventArgs"/> object containing the event data.</param>
        private void OnVoteShutdownExecuted(object sender, AdminCommandEventArgs e)
        {
            if (!this.enableVote.AsBool)
            {
                return;
            }

            if (VoteManager.StartVote($"{(this.autoRestart.AsBool ? "Restart" : "Shut down")} the server?"))
            {
                VoteManager.CurrentVote.Ended += this.OnShutdownVoteEnded;
            }
        }

        /// <summary>
        /// Called when the cancelshutdown admin command is executed.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="AdminCommandEventArgs"/> object containing the event data.</param>
        private void OnCancelShutdownExecuted(object sender, AdminCommandEventArgs e)
        {
            if (this.shutdownInProgress)
            {
                this.ScheduleNext();
                this.PrintToChatAll($"Server {(this.autoRestart.AsBool ? "restart" : "shutdown")} cancelled");

                if (!this.ShouldReplyToChat(e.SenderInfo.RemoteClientInfo))
                {
                    this.ReplyToCommand(e.SenderInfo, $"Server {(this.autoRestart.AsBool ? "restart" : "shutdown")} cancelled");
                }
            }
            else
            {
                this.ReplyToCommand(e.SenderInfo, $"No {(this.autoRestart.AsBool ? "restart" : "shutdown")} in progress");
            }
        }

        /// <summary>
        /// Called when a shutdown vote ends.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A <see cref="VoteEndedEventArgs"/> object containing the event data.</param>
        private void OnShutdownVoteEnded(object sender, VoteEndedEventArgs e)
        {
            if (e.Percents[0] >= this.votePercent.AsFloat)
            {
                this.PrintToChatAll($"Vote succeeded with {e.Percents[0] :P2} of the vote.", "Vote");
                this.PrintToChatAll($"{(this.autoRestart.AsBool ? "Restarting" : "Shutting down")} in 30 seconds...");
                if (this.shutdownTimer != null)
                {
                    this.shutdownTimer.Dispose();
                }

                this.shutdownInProgress = true;
                this.countdown = 0;
                this.shutdownTimer = new Timer(30000);
                this.shutdownTimer.Elapsed += this.OnShutdownTimerElapsed;
                this.shutdownTimer.Enabled = true;
            }
            else
            {
                this.PrintToChatAll($"Vote failed with {e.Percents[0] :P2} of the vote.", "Vote");
            }
        }

        /// <summary>
        /// Schedules the next automatic shutdown.
        /// </summary>
        private void ScheduleNext()
        {
            if (this.shutdownTimer != null)
            {
                this.shutdownTimer.Dispose();
                this.shutdownTimer = null;
            }

            this.shutdownInProgress = false;

            if (this.shutdownSchedule.Count == 0)
            {
                return;
            }

            this.countdown = 5;

            var time = this.shutdownSchedule.Find((int t) => (t - 5) >= DateTime.Now.TimeOfDay.TotalMinutes);
            time = time == 0 ? this.shutdownSchedule[0] : time;

            var dt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, time / 60, time % 60, 0).AddMinutes(-5);
            if (dt < DateTime.Now)
            {
                dt = dt.AddDays(1);
            }

            this.shutdownTimer = new Timer(dt.Subtract(DateTime.Now).TotalMilliseconds);
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
                this.shutdownInProgress = true;
                this.shutdownTimer = new Timer(60000);
                this.shutdownTimer.Elapsed += this.OnShutdownTimerElapsed;
                this.shutdownTimer.Enabled = true;

                if (this.countdown > 1)
                {
                    this.PrintToChatAll($"[{Colors.Yellow}]Warning: Server {(this.autoRestart.AsBool ? "restarting" : "shutting down")} in [i]{this.countdown} minutes[/i][-]");
                }
                else
                {
                    this.PrintToChatAll($"[{Colors.Red}]Warning: Server {(this.autoRestart.AsBool ? "restarting" : "shutting down")} down in [i]1 minute[/i][-]");
                    this.PrintToChatAll("Saving world state...");
                    this.ServerCommand("saveworld");
                }

                this.countdown--;
            }
            else
            {
                this.ServerCommand("shutdown");
                this.ScheduleNext();
            }
        }
    }
}
