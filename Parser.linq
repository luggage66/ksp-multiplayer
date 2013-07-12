<Query Kind="Program">
  <NuGetReference>Sprache</NuGetReference>
  <Namespace>Sprache</Namespace>
</Query>

void Main()
{
	//parameters
	string currentUser = "dj"; //not case sensitive
	var localSaveGameFolder = @"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\saves\test";
	var userSpecificFolder = Path.Combine(localSaveGameFolder, "userdata");
	var userSpecificFile = Path.Combine(userSpecificFolder, currentUser + ".sfs");
	var fileToSync = "persistent.sfs"; //also try quickssave
	
	//setup
	if (!Directory.Exists(userSpecificFolder)) Directory.CreateDirectory(userSpecificFolder);
	var fileConverter = new KspFileReader(); //loads and saves the format that sfs is. can read other file types, like .craft
	
	//load up my saved game
	var saveGameData = fileConverter.ReadFile(Path.Combine(localSaveGameFolder, fileToSync)); //get data
	var myUniverse = new Universe(saveGameData); //convert to "smart data" (understands the contents a bit)
	
	//clean it
	myUniverse.RemoveVessels(v => v.IsOwnedBy(currentUser));
	
	//resave as the "clean" version under my usename
	fileConverter.SaveFile(myUniverse.ToSaveGameObject(), userSpecificFile);
}

public class KspFileReader
{
	Parser<SaveGameObject> parser;
	
	public KspFileReader()
	{
		var newlineChars = Environment.NewLine.ToCharArray();
		var identifierChars = new char[] { '_' };
		
		var identifier = Parse.Char(c => char.IsLetterOrDigit(c) || identifierChars.Contains(c) ,"identifier character").Many().Token().Text();
		
		var separator = Parse.String("= ");
		var newline = Parse.String(Environment.NewLine).Text();
		var value = Parse.Char(c => !newlineChars.Contains(c), "non-newline").Many().Text();
		var beginBlock = Parse.Char('{').Token();
		var endBlock = Parse.Char('}').Token();
		
		var parameter = from i in identifier
						from _ in separator
						from v in value
						from _2 in newline
						select new SaveGameParameter() { Name = i, Value = v};
		
		Parser<SaveGameObject> section = null;
					
		section = from sectionName in identifier
					from _2 in beginBlock
					from parameters in parameter.Many().Select(x => x.ToList())
					from sections in Parse.Ref(() => section).Many().Select(x => x.ToList())
					from _3 in endBlock
					select new SaveGameObject() { Name = sectionName, Parameters = parameters, SubObjects = sections };
					
		parser = section;
	}
	
	public SaveGameObject ReadFile(string filename)
	{
		using (var file = new StreamReader(filename))
		{
			return parser.Parse(file.ReadToEnd());
		}
	}
	
	public void SaveFile(SaveGameObject data, string filename)
	{
		using (var writer = new StreamWriter(filename))
		{
			data.WriteToStream(writer);
		}
	}
}

public class Universe
{
	
	public IList<Vessel> Vessels { get; set; }	
	private SaveGameObject SaveGameObject { get; set; }
	public Vessel ActiveVessel { get; set; }
	
	public Universe(SaveGameObject saveGameObject)
	{
		SaveGameObject = saveGameObject;
		Prepare();
	}
		
	public void ImportVessels(Universe otherUniverse)
	{
		foreach (var v in otherUniverse.Vessels)
		{
			Vessels.Add(v);
		}
	}
	
	public void RemoveVessels(Predicate<Vessel> condition)
	{
		var vesselsToRemove = (
			from v in Vessels
			where condition(v)
			select v).ToList();
			
		vesselsToRemove.ForEach(v => Vessels.Remove(v));
	}
	
	public SaveGameObject ToSaveGameObject()
	{
		List<Crew> crewRoster = new List<Crew>();
		
		//add each USED crew member to the list
		foreach (var vessel in Vessels)
		{
			crewRoster.AddRange(vessel.Crew);
		}
		
		var flightState = (from fs in SaveGameObject.SubObjects where fs.Name == "FLIGHTSTATE"
		select fs).Single();
		
		flightState.SubObjects = new List<SaveGameObject>(); //only crew and vessels knows. just make a new collection
		
		//save all crew members used
		foreach (var crewMember in crewRoster)
		{
			flightState.SubObjects.Add(crewMember.ToSaveGameObject());
		}
		
		//save all ships (they fix their own crew indexes)
		foreach (var vessel in Vessels)
		{
			flightState.SubObjects.Add(vessel.ToSaveGameObject(crewRoster));
		}
	
		return SaveGameObject;
	}
	
	private void Prepare()
	{
		var flightState = (from fs in SaveGameObject.SubObjects where fs.Name == "FLIGHTSTATE" select fs).Single();
		
		//get all crew as loaded. needed for vessels
		var crewList = 
			(from crew in flightState.SubObjects where crew.Name == "CREW"
			select new Crew(crew))
			.ToList();
			
		//get all vessels
		Vessels = 
			(from vessel in flightState.SubObjects where vessel.Name == "VESSEL"
			select new Vessel(crewList, vessel))
			.ToList();
			
		var activeVesselIndex = int.Parse(flightState.Parameters.Where(p => p.Name == "activeVessel").Single().Value);
		ActiveVessel = Vessels[activeVesselIndex];
	}
}

public class Vessel
{
	SaveGameObject o;

	public IList<Crew> Crew { get; set; }

	public Vessel(IList<Crew> crewList, SaveGameObject saveGameObject)
	{
		this.o = saveGameObject;
		
		Crew = new List<Crew>();
		
		//add used crewmembers to the list
		(	from part in o.SubObjects where part.Name == "PART"
			from p in part.Parameters where p.Name == "crew"
			select crewList[int.Parse(p.Value)]).ToList().ForEach(c => Crew.Add(c));
		
	}
	
	public bool IsOwnedBy(string name)
	{
		return o.Parameters.Where(p => p.Name == "name").Single().Value.ToUpper().StartsWith(name.ToUpper() + ":");
	}
	
	public SaveGameObject ToSaveGameObject(IList<Crew> crewList)
	{
		var crewParameters = (from part in o.SubObjects where part.Name == "PART"
			from p in part.Parameters where p.Name == "crew"
			select p).ToList();
			
		for (int i = 0; i < Crew.Count; i++)
		{
			//set each one to the new index
			crewParameters[i].Value = crewList.IndexOf(Crew[i]).ToString();
		}
		
		
	
		return o;
	}
}

public class Crew
{
	SaveGameObject o;
	
	public Crew(SaveGameObject saveGameObject)
	{
		this.o = saveGameObject;
	}
	
	public SaveGameObject ToSaveGameObject()
	{
		return o;
	}
}

public class SaveGameObject 
{
	public string Name { get; set; }
	public IList<SaveGameParameter> Parameters { get; set; }
	public IList<SaveGameObject> SubObjects { get; set; }
	
	public void WriteToStream(StreamWriter writer, int level = 0)
	{
		var linePrefix = new string('\t', level);
		var subLevel = level + 1;
		var subLevelPrefix = new string('\t', subLevel);
		
		writer.WriteLine("{0}{1}", linePrefix, Name);
		writer.WriteLine("{0}{{", linePrefix);		
		foreach (var parameter in Parameters)
		{
			writer.WriteLine("{0}{1} = {2}", subLevelPrefix, parameter.Name, parameter.Value);	
		}

		foreach (var subObject in SubObjects)
		{
			subObject.WriteToStream(writer, subLevel);
		}
		writer.WriteLine("{0}}}", linePrefix);	
	}
}

public class SaveGameParameter //used instead of tuple so i can do reference checks.
{
	public string Name { get; set; }
	public string Value { get; set; }
}

// Define other methods and classes here