using shared;
using System;
using System.Collections.Generic;

public class GameData : ISerializable {
    public bool updated;

    // <passWord, Team>
    readonly Dictionary<string, GameTeam> teams = new Dictionary<string, GameTeam>();
    readonly Dictionary<string, GameTask> tasks = new Dictionary<string, GameTask>();

    public void addScore(string taskName, string teamPassWord, double score) {
        if (taskExists(taskName) && teamExists(teamPassWord)) 
            tasks[taskName].setScore(teams[teamPassWord], score);
        updated = true;
    }

    public bool taskExists(string name) {
        return tasks.ContainsKey(name);
    }

    public bool teamExists(string passWord) {
        return teams.ContainsKey(passWord);
    }

    public void CreateTask(string name) {
        if (tasks.ContainsKey(name)) throw new Exception("Name already in use");
        tasks.Add(name, new GameTask());
        updated = true;
    }

    public void CreateTeam(string name, string pass) {
        if (teams.ContainsKey(pass)) throw new Exception("Password already in use");
        teams.Add(pass, new GameTeam(name));
        updated = true;
    }

    public void Serialize(Packet pPacket) {
        pPacket.Write(teams.Count);
        pPacket.Write(tasks.Count);
        foreach (KeyValuePair<string, GameTeam> pair in teams) {
            pPacket.Write(pair.Key);
            pPacket.Write(pair.Value);
        }
        foreach (KeyValuePair<string, GameTask> pair in tasks) {
            pPacket.Write(pair.Key);
            pPacket.Write(pair.Value);
        }
    }

    public void Deserialize(Packet pPacket) {
        int teamCount = pPacket.ReadInt();
        int taskCount = pPacket.ReadInt();
        teams.Clear();
        tasks.Clear();
        while (teamCount > 0) {
            teams.Add(pPacket.ReadString(), (GameTeam)pPacket.ReadObject());
            teamCount--;
        }
        while (taskCount > 0) {
            tasks.Add(pPacket.ReadString(), (GameTask)pPacket.ReadObject());
            taskCount--;
        }
    }

    public void ConsoleLogTeams() {
        foreach (KeyValuePair<string,GameTeam> pair in teams) {
            Console.WriteLine($" - {pair.Value.name}, [{pair.Key}]");
        }
    }

    public void ConsoleLogTasks() {
        foreach (KeyValuePair<string, GameTask> pair in tasks) {
            Console.WriteLine($" - {pair.Key}");
        }
    }

    public GameTeam GetTeamData(string pass) {
        return teams[pass];
    }

    public int TeamAmount() {
        return teams.Count;
    }
    
    public int TaskAmount() {
        return tasks.Count;
    }
}

public class GameTeam : ISerializable{

    public string name { get; private set; }
    public int R { get; private set; }
    public int G { get; private set; }
    public int B { get; private set; }
    public GameTeam() { }
    public GameTeam(string name) { this.name = name; }

    public void Serialize(Packet pPacket) {
        pPacket.Write(name);

        pPacket.Write(R);
        pPacket.Write(G);
        pPacket.Write(B);
    }

    public void Deserialize(Packet pPacket) {
        name = pPacket.ReadString();
        R = pPacket.ReadInt();
        G = pPacket.ReadInt();
        B = pPacket.ReadInt();
    }
}

public class GameTask : ISerializable{
    private readonly Dictionary<GameTeam, double> scores = new Dictionary<GameTeam, double>();
    public bool hasTeamScore(GameTeam team) {
        return scores.ContainsKey(team);
    }

    public double getScore(GameTeam team) {
        if (!hasTeamScore(team)) return 0;
        return scores[team];
    }

    public void setScore(GameTeam team, double score) {
        scores.Add(team, score);
    }

    public void Serialize(Packet pPacket) {
        pPacket.Write(scores.Count);
        foreach (KeyValuePair<GameTeam,double> pair in scores) {
            pPacket.Write(pair.Key);
            pPacket.Write(pair.Value);
        }
    }

    public void Deserialize(Packet pPacket) {
        int scoreCount = pPacket.ReadInt();
        scores.Clear();
        while (scoreCount > 0) {
            scores.Add((GameTeam)pPacket.ReadObject(), pPacket.ReadDouble());
            scoreCount--;
        }
    }

}

