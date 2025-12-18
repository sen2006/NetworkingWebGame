
public class ConsoleCommands {
    static Exception ilegalArgumentCount = new Exception("Wrong number of arguments");

    public static void HandleCommand(string line) {
        string[] args = line.ToLower().Split(' ');
        try {
            switch (args[0]) {
                case "save": {
                    if (args.Length != 1) throw ilegalArgumentCount;
                    Server.saveGameData(Server.savePath);
                    Console.WriteLine($"Save succesfull");
                    break;
                };
                case "createteam": {
                    if (args.Length != 3) throw ilegalArgumentCount;
                    Server._gameData.CreateTeam(args[1], args[2]);
                    Console.WriteLine($"Team created Name:{args[1]} pass:{args[2]}");
                    break;
                };
                case "teams": {
                    if (args.Length != 1) throw ilegalArgumentCount;
                    Server._gameData.ConsoleLogTeams();
                    break;
                };
                case "createtask": {
                    if (args.Length != 2) throw ilegalArgumentCount;
                    Server._gameData.CreateTask(args[1]);
                    Console.WriteLine($"Task created Name:{args[1]}");
                    break;
                }
                case "tasks": {
                    if (args.Length != 1) throw ilegalArgumentCount;
                    Server._gameData.ConsoleLogTasks();
                    break;
                };
                default: {
                    throw new Exception("Unknown Command");
                };
            }
        } catch (Exception e) {
            Console.WriteLine(e.Message);
        }
        Console.WriteLine("");
    }

}

