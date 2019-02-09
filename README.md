# Server Shutdown

Plugin for SevenMod that schedules automatic server shutdowns and enables shutdown votes. This plugin is in early development and is not ready for use in a live environment. **Use at your own risk.**

## Configuration

File: **ServerShutdown.xml**

| Property                             | Default | Description                                                                              |
| ------------------------------------ | ------- | ---------------------------------------------------------------------------------------- |
| `ServerShutdownAutoRestart`          | `True`  | Enable if the server is set up to automatically restart after crashing                   |
| `ServerShutdownCountdownTime`        | `5`     | The countdown time in minutes for scheduled shutdowns                                    |
| `ServerShutdownEnableRestartCommand` | `True`  | Enable the restart admin command                                                         |
| `ServerShutdownEnableVote`           | `True`  | Enable the voteshutdown admin command                                                    |
| `ServerShutdownSchedule`             | `""`    | The automatic shutdown schedule in the format HH:MM. Separate multiple times with commas |
| `ServerShutdownVotePercent`          | `0.60`  | The percentage of players that must vote yes for a successful shutdown vote              |

## Admin Commands

| Command             | Arguments (_\<required\> [optional]_) | Access    | Description                           |
| ------------------- | ------------------------------------- | --------- | ------------------------------------- |
| `sm cancelrestart`  |                                       | Changemap | Cancels an impending restart          |
| `sm cancelshutdown` |                                       | Changemap | Cancels an impending shutdown         |
| `sm restart`        | [minutes]                             | RCON      | Starts a server restart               |
| `sm voterestart`    |                                       | Vote      | Starts a vote to restart the server   |
| `sm voteshutdown`   |                                       | Vote      | Starts a vote to shut down the server |

## License

The source code for SevenMod is available under the terms of the [MIT License](https://github.com/SevenMod/Plugin-ServerShutdown/blob/master/LICENSE.txt).
See the LICENSE.txt in the project root for details.