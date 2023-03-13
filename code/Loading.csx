#nullable enable

#load "Consts.csx"
#load "DataStructures.csx"

#r "nuget: CsvHelper, 30.0.1"
using CsvHelper;
using CsvHelper.Configuration;
using System.Text.Json;
using System.Globalization;


readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
{
    DetectDelimiter = true
};


async Task<Config> LoadConfig()
    => JsonSerializer.Deserialize<Config>(await File.ReadAllTextAsync(CONFIG_PATH)) ?? throw new Exception("Can't load config");

List<T> LoadData<T>(string file)
{
    using (var reader = new StreamReader(file))
    using (var csv = new CsvReader(reader, CsvConfig))
    {
        return csv.GetRecords<T>().ToList();
    }
}
List<Driver> LoadDrivers() => LoadData<DriverCSV>(DRIVERSDB_PATH).Select(x => new Driver(x.Driver, x.Team, x.Price, x.Sum)).ToList();
List<Team> LoadTeams() => LoadData<TeamCSV>(TEAMSDB_PATH).Select(x => new Team(x.Team, x.Cost, x.Sum)).ToList();