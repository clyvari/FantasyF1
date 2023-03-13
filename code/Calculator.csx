#load "Combinatorics.csx"
#load "DataStructures.csx"

using System.Collections.Immutable;

class Calculator
{
    Config Config { get; }
    ImmutableList<Driver> DriversDb { get; }
    ImmutableList<Team> TeamsDb { get; }
    F1Team CurrentTeam { get; }
    List<F1Team> StatesDb { get; set; } = new();

    public Calculator(Config config, IEnumerable<Driver> driversDb, IEnumerable<Team> teamsDb)
    {
        Config = config;
        DriversDb = driversDb.ToImmutableList();
        TeamsDb = teamsDb.ToImmutableList();

        CheckDataIntegrity();

        CurrentTeam = GetCurrentTeam();
    }

    void CheckDataIntegrity()
    {
        if(DriversDb.Count() != 20 && DriversDb.DistinctBy(x => x.Id).Count() != 20) throw new Exception("20 drivers required");
        if(TeamsDb.Count() != 10 && TeamsDb.DistinctBy(x => x.Id).Count() != 10) throw new Exception("10 teams required");

        var byTeam = DriversDb.ToLookup(x => x.Team);
        if(byTeam.Any(x => x.Count() != 2)) throw new Exception("Each team must have 2 driver");
        if(!byTeam.All(x => TeamsDb.Any(y => y.Id == x.Key))) throw new Exception("Some teams don't match driver's teams");

        if(Config.CurrentTeam.Drivers.Length != 5) throw new Exception("Need 5 current drivers");
        if(Config.CurrentTeam.Teams.Length != 2) throw new Exception("Need 2 current teams");

        if(!Config.CurrentTeam.Drivers.All(x => DriversDb.Any(y => y.Id == x))) throw new Exception("Current drivers doesn't match drivers db");
        if(!Config.CurrentTeam.Teams.All(x => TeamsDb.Any(y => y.Id == x))) throw new Exception("Current teams doesn't match teams db");


        System.Console.WriteLine("Everything checks out");
    }

    F1Team GetCurrentTeam()
    {
        var drivers = DriversDb.Where(x => Config.CurrentTeam.Drivers.Contains(x.Id));
        var teams = TeamsDb.Where(x => Config.CurrentTeam.Teams.Contains(x.Id));

        var t = new F1Team(drivers, teams, Config.CurrentTeam.CostCap);
        Console.WriteLine($"Current Team: \n{t}");
        Console.WriteLine($"Current cost: ${t.Cost}/${t.CostCap}, ${t.CostCap - t.Cost} left");
        Console.WriteLine($"IsValid: {t.IsValid}");
        return t;
    }

    List<F1Team> MapBitsToTeams(List<ulong> bits)
    {
        var res = new List<F1Team>(bits.Count);
        foreach(var d in bits)
        {
            var drvset = d >> 32;
            var drv = DriversDb.Where((x, i) => (((ulong)1 << i) & drvset) != 0);
            var tms = TeamsDb.Where((x, i) => (((ulong)1 << i) & d) != 0);
            res.Add(new(drv.ToImmutableList(), tms.ToImmutableList(), Config.CurrentTeam.CostCap));
        }
        return res;
    }

    public void LoadPossibleTeams()
    {
        var sw = new Stopwatch();
        sw.Start();
        var drvBits = Combinatorics.GenerateCombinations(20,5);
        sw.Stop();
        System.Console.WriteLine($"Generated {drvBits.Count} possibilties for drivers in {sw.Elapsed}");
        sw.Restart();
        var tmBits = Combinatorics.GenerateCombinations(10,2);
        sw.Stop();
        System.Console.WriteLine($"Generated {tmBits.Count} possibilties for teams in {sw.Elapsed}");
        sw.Restart();
        var drv_tm = Combinatorics.Combine(drvBits, tmBits);
        sw.Stop();
        System.Console.WriteLine($"Generated {drv_tm.Count} states in {sw.Elapsed}");
        sw.Restart();
        if(drv_tm.Distinct().Count() != drv_tm.Count()) throw new Exception("Duplicates !!");
        sw.Stop();
        System.Console.WriteLine($"Checked for duplicates in {sw.Elapsed}");
        sw.Restart();
        StatesDb = MapBitsToTeams(drv_tm);
        sw.Stop();
        System.Console.WriteLine($"Converted states to Teams in {sw.Elapsed}");
    }

    List<(int nbChange, F1Team team)> ComputeNumberOfChanges(List<F1Team> teams)
    {
        return teams.Select(x => ( x.Drivers.Where(y => !CurrentTeam.Drivers.Contains(y)).Count()
                                + x.Teams.Where(y => !CurrentTeam.Teams.Contains(y)).Count(), x )  ).ToList();
    }

    public void DisplayResults()
    {
        System.Console.WriteLine();
        System.Console.WriteLine("Overall best teams:");
        foreach(var t in StatesDb.OrderByDescending(x => x.Points).Take(10))
        {
            System.Console.WriteLine($"{t} - p{t.Points} - ${t.Cost}");
        }

        System.Console.WriteLine();
        System.Console.WriteLine();

        var validTeams = StatesDb.Where(x => x.IsValid).ToList();
        System.Console.WriteLine($"{validTeams.Count} valid teams");
        var betterTeams = validTeams.Where(x => x.Points > CurrentTeam.Points).ToList();
        System.Console.WriteLine($"Found {betterTeams.Count} better teams (current: {CurrentTeam.Points})");
        foreach(var t in betterTeams.OrderByDescending(x => x.Points).Take(10))
        {
            System.Console.WriteLine($"{t} - p{t.Points} - ${t.Cost}");
        }

        var teamsWithMalus = ComputeNumberOfChanges(betterTeams)
                                .Select(x => (truePoints: x.team.Points - (4*Math.Max(0, x.nbChange-2)), x.nbChange, x.team))
                                .OrderByDescending(x => x.truePoints);
        System.Console.WriteLine();
        System.Console.WriteLine();
        foreach(var t in teamsWithMalus.Take(10))
        {
            System.Console.WriteLine($"{t.team} - p{t.team.Points} - tp{t.truePoints} - nb{t.nbChange} - ${t.team.Cost}");
        }
    }

}