#!/bin/env -S dotnet script
#nullable enable
#r "nuget: CsvHelper, 30.0.1"
using System.Collections.Immutable;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

static readonly string[] CurrentDrivers = new[]{ "ALO", "VER", "OCO", "BOT", "NOR" };
static readonly string[] CurrentTeams = new[] { "RB", "AM" };
const decimal CurrentCostCap = 100.3m;

static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
{
    DetectDelimiter = true
};

record DriverCSV(string Driver, string Team, decimal Price, decimal Sum);
record Driver(string Id, string Team, decimal Cost, decimal Points)
{
    public override string ToString() => $"{Id}";
    public virtual bool Equals(Driver? obj) => obj?.Id == Id;
    public override int GetHashCode() => Id.GetHashCode();
}
record TeamCSV(string Team, decimal Cost, decimal Sum);
record Team(string Id, decimal Cost, decimal Points)
{
    public override string ToString() => (Id + new string('_', Math.Max(0, 3-Id.Length))).Substring(0,3);
    public virtual bool Equals(Team? obj) => obj?.Id == Id;
    public override int GetHashCode() => Id.GetHashCode();
}

List<T> LoadData<T>(string file)
{
    using (var reader = new StreamReader(file))
    using (var csv = new CsvReader(reader, CsvConfig))
    {
        return csv.GetRecords<T>().ToList();
    }
}
List<Driver> LoadDrivers() => LoadData<DriverCSV>("drivers.csv").Select(x => new Driver(x.Driver, x.Team, x.Price, x.Sum)).ToList();
List<Team> LoadTeams() => LoadData<TeamCSV>("teams.csv").Select(x => new Team(x.Team, x.Cost, x.Sum)).ToList();

var drivers = LoadDrivers();
System.Console.WriteLine($"Loaded {drivers.Count()} drivers");
var teams = LoadTeams();
System.Console.WriteLine($"Loaded {teams.Count()} teams");


bool CheckDataIntegrity(IEnumerable<Driver> drivers, IEnumerable<Team> teams)
{
    if(drivers.Count() != 20 && drivers.DistinctBy(x => x.Id).Count() != 20) throw new Exception("20 drivers required");
    if(teams.Count() != 10 && teams.DistinctBy(x => x.Id).Count() != 10) throw new Exception("10 teams required");

    var byTeam = drivers.ToLookup(x => x.Team);
    if(byTeam.Any(x => x.Count() != 2)) throw new Exception("Each team must have 2 driver");
    if(!byTeam.All(x => teams.Any(y => y.Id == x.Key))) throw new Exception("Some teams don't match driver's teams");

    if(CurrentDrivers.Length != 5) throw new Exception("Need 5 current drivers");
    if(CurrentTeams.Length != 2) throw new Exception("Need 2 current teams");

    if(!CurrentDrivers.All(x => drivers.Any(y => y.Id == x))) throw new Exception("Current drivers doesn't match drivers db");
    if(!CurrentTeams.All(x => teams.Any(y => y.Id == x))) throw new Exception("Current teams doesn't match teams db");


    System.Console.WriteLine("Everything checks out");
    return true;
}

CheckDataIntegrity(drivers, teams);

record F1Team(ImmutableList<Driver> Drivers, ImmutableList<Team> Teams)
{
    public bool IsComplete => Drivers.Count == 5 && Teams.Count == 2;
    public decimal Cost() => Drivers.Sum(x => x.Cost) + Teams.Sum(x => x.Cost);
    public decimal Points() => Drivers.Sum(x => x.Points) + Teams.Sum(x => x.Points);
    public bool IsValid() => IsComplete && Cost() <= CurrentCostCap;
    public override string ToString() => $"{string.Join(",", Drivers)}|{string.Join(",", Teams)}"; 
    public virtual bool Equals(F1Team? obj)
    {
        if(obj is null) return false;
        return Drivers.All(obj.Drivers.Contains) && Drivers.Count == obj.Drivers.Count
               && Teams.All(obj.Teams.Contains) && Teams.Count == obj.Teams.Count;
    }
    public override int GetHashCode()
    {
        return Drivers.Select(x => x.GetHashCode())
                      .Aggregate((res, x) => x^res)
               ^Teams.Select(x => x.GetHashCode())
                     .Aggregate((res, x) => x^res);
    }
}
record F1Pool(ImmutableList<Driver> Drivers, ImmutableList<Team> Teams)
{
    public override string ToString() => $"{string.Join(",", Drivers)}|{string.Join(",", Teams)}";
}
record F1State(F1Team CurrentTeam, F1Pool Pool)
{
    public bool IsComplete => (CurrentTeam.Drivers.Count + Pool.Drivers.Count == 20)
                              && (CurrentTeam.Teams.Count + Pool.Teams.Count == 10);
    public decimal Cost() => CurrentTeam.Cost();
    public bool IsValid() => IsComplete && CurrentTeam.IsValid();
}

F1State GetCurrentState(IEnumerable<Driver> allDrivers, IEnumerable<Team> allTeams)
{
    var drvMap = allDrivers.ToLookup(x => CurrentDrivers.Contains(x.Id));
    var tmMap = allTeams.ToLookup(x => CurrentTeams.Contains(x.Id));

    var f1t = new F1Team(drvMap[true].ToImmutableList(), tmMap[true].ToImmutableList());
    var pool = new F1Pool(drvMap[false].ToImmutableList(), tmMap[false].ToImmutableList());

    return new F1State(f1t, pool);
}


var currentState = GetCurrentState(drivers, teams);
Console.WriteLine($"Current Team: \n{currentState.CurrentTeam}");
Console.WriteLine($"Current cost: ${currentState.CurrentTeam.Cost()}/${CurrentCostCap}, ${CurrentCostCap - currentState.CurrentTeam.Cost()} left");
Console.WriteLine($"IsValid: {currentState.CurrentTeam.IsValid()}");

F1State RemoveDriverFromCurrentTeam(F1State input, Driver d)
    => new F1State(
        new(input.CurrentTeam.Drivers.Remove(d), input.CurrentTeam.Teams),
        new(input.Pool.Drivers.Add(d), input.Pool.Teams)
    );
F1State RemoveTeamFromCurrentTeam(F1State input, Team t)
    => new F1State(
        new(input.CurrentTeam.Drivers, input.CurrentTeam.Teams.Remove(t)),
        new(input.Pool.Drivers, input.Pool.Teams.Add(t))
    );
F1State AddDriverToCurrentTeam(F1State input, Driver d)
    => new F1State(
        new(input.CurrentTeam.Drivers.Add(d), input.CurrentTeam.Teams),
        new(input.Pool.Drivers.Remove(d), input.Pool.Teams)
    );
F1State AddTeamToCurrentTeam(F1State input, Team t)
    => new F1State(
        new(input.CurrentTeam.Drivers, input.CurrentTeam.Teams.Add(t)),
        new(input.Pool.Drivers, input.Pool.Teams.Remove(t))
    );

List<F1State> AddOrRemoveElementsFromCurrentTeam(F1State input, int numberOfDrivers, int numberOfTeams, bool add = false)
{
    if((numberOfDrivers+numberOfTeams) < 1 ){
        //System.Console.WriteLine("DROP: 0,0 -> *");
        return new List<F1State>{ input };
    }

    

    var nextNumOfDrive = numberOfDrivers > 0 ? numberOfDrivers-1 : 0;
    var nextNumOfTeam = numberOfDrivers < 1 ? (numberOfTeams > 0 ? numberOfTeams-1 : 0) : numberOfTeams;

    //System.Console.WriteLine($"DROP: {numberOfDrivers},{numberOfTeams} -> {nextNumOfDrive},{nextNumOfTeam}");
    
    IEnumerable<F1State> newStates;

    var driversToConsider = add ? input.Pool.Drivers : input.CurrentTeam.Drivers;
    var teamsToConsider = add ? input.Pool.Teams : input.CurrentTeam.Teams;

    if(numberOfDrivers > 0)
    {
        newStates = driversToConsider.Select(d => add ? AddDriverToCurrentTeam(input, d) : RemoveDriverFromCurrentTeam(input, d));
    }
    else
    {
        newStates = teamsToConsider.Select(t => add ? AddTeamToCurrentTeam(input, t) : RemoveTeamFromCurrentTeam(input, t));
    }
    
    return newStates.SelectMany(x => AddOrRemoveElementsFromCurrentTeam(x, nextNumOfDrive, nextNumOfTeam, add))
                            .ToList();
    
}

List<F1State> AddOrRemoveElementsFromCurrentTeam(F1State input, int number, bool add = false)
{
    if(number < 1) return new List<F1State> { input };

    return Enumerable.Range(0, number+1)
                     .Select(x => (d: x, t: number-x))
                     .Where(x => x.d <= 5 && x.t <= 2)
                     .SelectMany(x => AddOrRemoveElementsFromCurrentTeam(input, x.d, x.t, add))
                     .ToList();

}

List<F1State> DropAtMostElementsFromCurrentTeam(F1State input, int number)
{
    return Enumerable.Range(0, number+1)
                     .SelectMany(x => AddOrRemoveElementsFromCurrentTeam(input, x))
                     .DistinctBy(x => x.CurrentTeam.ToString())
                     .ToList();
}

List<F1State> GetPossibleNewStates(F1State inputState)
{
    var res = DropAtMostElementsFromCurrentTeam(inputState, 7)
                .SelectMany(x => AddOrRemoveElementsFromCurrentTeam(x, 5-x.CurrentTeam.Drivers.Count, 2-x.CurrentTeam.Teams.Count , true))
                .DistinctBy(x => x.CurrentTeam)
                .ToList();

    System.Console.WriteLine($"Generated {res.Count} possible states");

    if(res.Any(x => !x.IsComplete)) throw new Exception("Some states are not complete");
    
    var validStates = res.Where(x => x.IsValid()).ToList();
    System.Console.WriteLine($"Throwing out {res.Count - validStates.Count} invalid states, {validStates.Count} left");

    return validStates;
}

// var possibleStates = GetPossibleNewStates(currentState);



// var currentPoints = currentState.CurrentTeam.Points();
// System.Console.WriteLine($"Current Score threshold: {currentPoints}");
// var goodStates = possibleStates.Where(x => x.CurrentTeam.Points() > currentPoints )
//                                .OrderByDescending(x => x.CurrentTeam.Points())
//                                .ToList();
// System.Console.WriteLine($"Remaining {goodStates.Count} better states");
// System.Console.WriteLine($"Current team:\n\t{currentState.CurrentTeam}\t{currentPoints}");
// System.Console.WriteLine($"Top 10 other teams:\n\t{string.Join("\n\t", goodStates.Select(x => $"{x.CurrentTeam}\t{x.CurrentTeam.Points()}").Take(10))}");


List<(ImmutableArray<byte> A, ImmutableArray<byte> B)> results = new();

void GeneratePerms((ImmutableArray<byte> currentA, ImmutableArray<byte> currentB,
                   ImmutableArray<byte> poolA, ImmutableArray<byte> poolB) state,
                   byte addToA,
                   byte addToB)
{
    if((addToA+addToB) < 1 ) { /*results.Add((state.currentA, state.currentB)); return;*/ throw new Exception(); }
    
    byte newAddToA = (byte)(addToA > 0 ? addToA-1 : 0);
    byte newAddToB = (byte)(addToA < 1 ? (addToB > 0 ? addToB-1 : 0) : addToB);
    
    List<(ImmutableArray<byte> currentA, ImmutableArray<byte> currentB,
                   ImmutableArray<byte> poolA, ImmutableArray<byte> poolB)> newStates;

    if(addToA > 0)
    {
        newStates = state.poolA.Select(d => (state.currentA.Add(d), state.currentB, state.poolA.Remove(d), state.poolB)).ToList();
    }
    else
    {
        newStates = state.poolB.Select(d => (state.currentA, state.currentB.Add(d), state.poolA, state.poolB.Remove(d))).ToList();
    }

    foreach(var s in newStates)
    {
        if((newAddToA+newAddToB) < 1)
        {
            results.Add((s.currentA, s.currentB));
        }
        else
        {
            GeneratePerms(s, newAddToA, newAddToB);
        }
    }
}

// var initState = (
//     ImmutableArray<byte>.Empty,
//     ImmutableArray<byte>.Empty,
//     ImmutableArray<byte>.Empty.AddRange(Enumerable.Range(0, 20).Select(x => (byte)x)),
//     ImmutableArray<byte>.Empty.AddRange(Enumerable.Range(0, 10).Select(x => (byte)x))
// );

// GeneratePerms(initState, 4, 2);
// System.Console.WriteLine(results.Count);

List<uint> GenerateStates(int maxBitIndex = 20, int numOfBit = 5)
{
    var res = new List<uint>();
    var config = Enumerable.Range(0, numOfBit).Reverse().ToArray();
    var notDone = true;
    while (notDone)
    {
        uint data = 0;
        int packIndex = -1;
        var hasIncremented = false;
        for(var i = 0; i < config.Length; i++)
        {
            data |= (uint)1 << config[i];
            if(!hasIncremented)
            {
                if(config[i] < (maxBitIndex-i-1))
                {
                    config[i]++;
                    hasIncremented = true;
                    packIndex = i;
                }
            }
        }
        for(var i = 0; i < packIndex; i++)
        {
            config[i] = config[packIndex]+packIndex-i;
        }
        res.Add(data);
        if(!hasIncremented) notDone = false;
    }
    return res;
}

var drvBits = GenerateStates(20,5);
var tmBits = GenerateStates(10,2);

List<ulong> Combine(List<uint> al, List<uint> bl)
{
    var res = new List<ulong>(al.Count*bl.Count);
    foreach(var a in al)
    {
        foreach(var b in bl)
        {
            res.Add(((ulong)a) << 32 | b);
        }
    }
    return res;
}

// System.Console.WriteLine(drvBits.Count);
// System.Console.WriteLine(tmBits.Count);
// foreach (var a in tmBits)
// {
//     System.Console.WriteLine(Convert.ToString(a, 2).PadLeft(10,'0'));
// }

var drv_tm = Combine(drvBits, tmBits);
// System.Console.WriteLine(drv_tm.Count);

List<F1Team> MapBitsToTeams(List<ulong> bits)
{
    var res = new List<F1Team>(bits.Count);
    foreach(var d in bits)
    {
        var drvset = d >> 32;
        var drv = drivers.Where((x, i) => (((ulong)1 << i) & drvset) != 0);
        var tms = teams.Where((x, i) => (((ulong)1 << i) & d) != 0);
        res.Add(new(drv.ToImmutableList(), tms.ToImmutableList()));
    }
    return res;
}

var mappedteams = MapBitsToTeams(drv_tm);
// if(mappedteams.Any(x => !x.IsComplete)) throw new Exception();
// System.Console.WriteLine("All teams complete");

System.Console.WriteLine();
System.Console.WriteLine("Overall best teams:");
foreach(var t in mappedteams.OrderByDescending(x => x.Points()).Take(10))
{
    System.Console.WriteLine($"{t} - p{t.Points()} - ${t.Cost()}");
}

System.Console.WriteLine();
System.Console.WriteLine();

var validTeams = mappedteams.Where(x => x.IsValid()).ToList();
System.Console.WriteLine($"{validTeams.Count} valid teams");
var betterTeams = validTeams.Where(x => x.Points() > currentState.CurrentTeam.Points()).ToList();
System.Console.WriteLine($"Found {betterTeams.Count} better teams (current: {currentState.CurrentTeam.Points()})");
foreach(var t in betterTeams.OrderByDescending(x => x.Points()).Take(10))
{
    System.Console.WriteLine($"{t} - p{t.Points()} - ${t.Cost()}");
}

List<(int nbChange, F1Team team)> ComputeNumberOfChanges(List<F1Team> allTeams)
{
    return allTeams.Select(x => ( x.Drivers.Where(y => !currentState.CurrentTeam.Drivers.Contains(y)).Count()
                            + x.Teams.Where(y => !currentState.CurrentTeam.Teams.Contains(y)).Count(), x )  ).ToList();
}

var teamsWithMalus = ComputeNumberOfChanges(betterTeams)
                        .Select(x => (truePoints: x.team.Points() - (4*Math.Max(0, x.nbChange-2)), x.nbChange, x.team))
                        .OrderByDescending(x => x.truePoints);
System.Console.WriteLine();
System.Console.WriteLine();
foreach(var t in teamsWithMalus.Take(10))
{
    System.Console.WriteLine($"{t.team} - p{t.team.Points()} - tp{t.truePoints} - nb{t.nbChange} - ${t.team.Cost()}");
}

// var noOneState = new F1State(new(new List<Driver>().ToImmutableList(), new List<Team>().ToImmutableList()),
//                              new(currentState.Pool.Drivers.AddRange(currentState.CurrentTeam.Drivers),
//                                     currentState.Pool.Teams.AddRange(currentState.CurrentTeam.Teams)));



// System.Console.WriteLine(noOneState.CurrentTeam);
// System.Console.WriteLine(noOneState.Pool);

// var allPossibleStates = AddOrRemoveElementsFromCurrentTeam(noOneState, 4, 2 , true);
// System.Console.WriteLine(allPossibleStates.Count);


/// tests
// var _1_team_removed = RemoveTeamFromCurrentTeam(currentState, currentState.CurrentTeam.Teams[0]);

// // System.Console.WriteLine($"-1t: {AddOrRemoveElementsFromCurrentTeam(_1_team_removed, 0, 1, true).Count}");

// // var _1_driver_removed = RemoveDriverFromCurrentTeam(currentState, currentState.CurrentTeam.Drivers[0]);
// // System.Console.WriteLine($"-1d: {AddOrRemoveElementsFromCurrentTeam(_1_driver_removed, 1, 0, true).Count}");

// var _2_teams_removed = RemoveTeamFromCurrentTeam(_1_team_removed, _1_team_removed.CurrentTeam.Teams[0]);
// System.Console.WriteLine($"-2t: {AddOrRemoveElementsFromCurrentTeam(_2_teams_removed, 0, 2, true).Count}");

// var a = new Driver("ALO", "TEST", 0, 0);
// var b = new Driver("ALP", "TEST", 0, 0);
// var c = new Driver("ALO", "asd", 1, 1);
// System.Console.WriteLine($"{a == b}");
// System.Console.WriteLine($"{a == c}");

// var rmT = currentState.CurrentTeam.Teams[0];
// var n = AddTeamToCurrentTeam(RemoveTeamFromCurrentTeam(currentState, rmT), rmT);
// System.Console.WriteLine(currentState.CurrentTeam);
// System.Console.WriteLine(n.CurrentTeam);
// System.Console.WriteLine(currentState.CurrentTeam == n.CurrentTeam);

// var n2 = AddTeamToCurrentTeam(RemoveTeamFromCurrentTeam(currentState, rmT), currentState.Pool.Teams[0]);
// System.Console.WriteLine(currentState.CurrentTeam);
// System.Console.WriteLine(n2.CurrentTeam);
// System.Console.WriteLine(currentState.CurrentTeam == n2.CurrentTeam);

// var rmD = currentState.CurrentTeam.Drivers[0];
// var m = AddDriverToCurrentTeam(RemoveDriverFromCurrentTeam(currentState, rmD), rmD);
// System.Console.WriteLine(currentState.CurrentTeam);
// System.Console.WriteLine(m.CurrentTeam);
// System.Console.WriteLine(currentState.CurrentTeam == m.CurrentTeam);

// var m2 = AddDriverToCurrentTeam(RemoveDriverFromCurrentTeam(currentState, rmD), currentState.Pool.Drivers[0]);
// System.Console.WriteLine(currentState.CurrentTeam);
// System.Console.WriteLine(m2.CurrentTeam);
// System.Console.WriteLine(currentState.CurrentTeam == m2.CurrentTeam);

