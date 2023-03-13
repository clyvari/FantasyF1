#nullable enable
using System.Collections.Immutable;

record JsonTeam(string[] Drivers, string[] Teams, decimal CostCap);
record Config(JsonTeam CurrentTeam);

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

record F1Team
{
    public F1Team(IEnumerable<Driver> drivers, IEnumerable<Team> teams, decimal costCap)
    {
        Drivers = drivers.ToImmutableList();
        Teams = teams.ToImmutableList();
        CostCap = costCap;

        IsComplete = Drivers.Count == 5 && Teams.Count == 2;
        Cost = Drivers.Sum(x => x.Cost) + Teams.Sum(x => x.Cost);
        Points = Drivers.Sum(x => x.Points) + Teams.Sum(x => x.Points);
        IsValid = IsComplete && Cost <= CostCap;
    }
    
    public bool IsComplete { get; }
    public ImmutableList<Driver> Drivers { get; }
    public ImmutableList<Team> Teams { get; }
    public decimal CostCap { get; }
    public decimal Cost { get; }
    public decimal Points { get; }
    public bool IsValid { get; }

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