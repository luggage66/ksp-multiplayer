<Query Kind="Statements">
  <NuGetReference>Sprache</NuGetReference>
  <Namespace>Sprache</Namespace>
</Query>

string fileContents;

var filename = @"C:\Users\dmull\Downloads\ksp-win-0.18.4\KSP_win\saves\default\persistent.sfs";

using (var file = new StreamReader(filename))
{
	fileContents = file.ReadToEnd();
}

var identifier = Parse.Letter.Many().Token().Text();
var separator = Parse.Char('=').Token();
var value = Parse.Not(Parse.Char('\n'));
var beginBlock = Parse.Char('{').Token();
var endBlock = Parse.Char('}').Token();

var parameter = from i in identifier
				from _ in separator
				from v in value
				select new { i, v };
				

				
//var parameterOrBlock = parameter.Or(block);

fileContents.Dump();

"something = a value"




