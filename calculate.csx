#!/bin/env -S dotnet script

#nullable enable

#load "code/Loading.csx"
#load "code/Calculator.csx"

var currentConfig = await LoadConfig();
System.Console.WriteLine("Loaded config");
var drivers = LoadDrivers();
System.Console.WriteLine($"Loaded {drivers.Count()} drivers");
var teams = LoadTeams();
System.Console.WriteLine($"Loaded {teams.Count()} teams");


var calc = new Calculator(currentConfig, drivers, teams);
calc.LoadPossibleTeams();
calc.DisplayResults();


