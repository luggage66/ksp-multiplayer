<Query Kind="Program">
  <NuGetReference>Sprache</NuGetReference>
  <Namespace>Sprache</Namespace>
</Query>

void Main()
{
	string fileContents;
	
	var filename = @"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\saves\default\persistent.sfs";
	
	using (var file = new StreamReader(filename))
	{
		fileContents = file.ReadToEnd();
	}
	
	var identifier = Parse.LetterOrDigit.Many().Token().Text();
	var separator = Parse.Char('=').Token();
	var newline = Parse.String(Environment.NewLine).Text();
	var value = Parse.AnyChar.Except(newline).AtLeastOnce().Text();
	var beginBlock = Parse.Char('{').Token();
	var endBlock = Parse.Char('}').Token();
	
	var parameter = from i in identifier
					from _ in separator
					from v in value
					from _2 in newline
					select new KeyValuePair<string, string>(i, v);
	
	
	Parser<SaveGameObject> sectionContents = null;
	
	Parser<SaveGameObject> section = null;
				
	section = from sectionName in identifier
				from _2 in beginBlock
				from parameters in parameter.Many().Select(ListToDict)
				from sections in Parse.Ref(() => section).Many()
				from _3 in endBlock
				select new SaveGameObject() { Name = sectionName, Parameters = parameters, SubObjects = sections };
				
	var sfsFile = section;
					
					
	
					
	//var parameterOrBlock = parameter.Or(block);
	
	
	
	
	sfsFile.Parse(@"GAME
{
	version = 0.19.1
	Title = default (Sandbox)
	Description = No description available.
	Mode = 0
	Status = 1
	scene = 5
	PARAMETERS
	{
		FLIGHT
		{
			CanQuickSave = True
			CanQuickLoad = True
			CanAutoSave = True
			CanUseMap = True
			CanSwitchVesselsNear = True
			CanSwitchVesselsFar = True
			CanTimeWarpHigh = True
			CanTimeWarpLow = True
			CanEVA = True
			CanIVA = True
		}
	}
}").Dump();
	
	sfsFile.Parse(fileContents).Dump();
	
	
	//fileContents.Dump();
}

public class SaveGameObject 
{
	public string Name { get; set; }
	public IDictionary<string, string> Parameters { get; set; }
	public IEnumerable<SaveGameObject> SubObjects { get; set; }
}

IDictionary<K,V> ListToDict<K,V>(IEnumerable<KeyValuePair<K,V>> input)
{
	var retval = new Dictionary<K,V>();
	
	foreach (var x in input)
	{
		if (!retval.ContainsKey(x.Key))
			retval.Add(x.Key, x.Value);		
	}
	
	return retval;
}

// Define other methods and classes here