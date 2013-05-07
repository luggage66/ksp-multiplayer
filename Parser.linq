<Query Kind="Program">
  <NuGetReference>Sprache</NuGetReference>
  <Namespace>Sprache</Namespace>
</Query>

void Main()
{
	var sourceFile = @"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\saves\test\persistent.sfs";
	var destFile = @"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\saves\default\persistent.sfs";

	var saveGameParser = SetupParser();

	var source = saveGameParser.Parse(FiletoString(sourceFile));
	var dest = saveGameParser.Parse(FiletoString(destFile));
	
	var destUniverse = new Universe(dest);
	var sourceUniverse = new Universe(source);
	
	destUniverse.MergeVessels(sourceUniverse);
		
	using (var writer = new StreamWriter(destFile))
	{
		destUniverse.ToSaveGameObject().WriteToStream(writer);
	}
	destUniverse.Dump();
	

			
	
}

Universe Merge(Universe mine, Universe theirs)
{
	return null;
}

public string FiletoString(string filename)
{
	using (var file = new StreamReader(filename))
	{
		return file.ReadToEnd();
	}
}

public Parser<SaveGameObject> SetupParser()
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
				
	return section;
}

//
public class Universe
{
	
	public IList<Vessel> Vessels { get; set; }	
	private SaveGameObject SaveGameObject { get; set; }
	
	public Universe(SaveGameObject saveGameObject)
	{
		SaveGameObject = saveGameObject;
		Prepare();
	}
	
	public void MergeVessels(Universe otherUniverse)
	{
		foreach (var v in otherUniverse.Vessels)
		{
			Vessels.Add(v);
		}
	}
	
	public SaveGameObject ToSaveGameObject()
	{
		List<Crew> crewRoster = new List<Crew>();
		
		foreach (var vessel in Vessels)
		{
			crewRoster.AddRange(vessel.Crew);
		}
		
		var flightState = (from fs in SaveGameObject.SubObjects where fs.Name == "FLIGHTSTATE"
		select fs).Single();
		
		flightState.SubObjects = new List<SaveGameObject>(); //only crew and vessels knows. just make a new collection
		
		foreach (var crewMember in crewRoster)
		{
			flightState.SubObjects.Add(crewMember.ToSaveGameObject());
		}
		
		foreach (var vessel in Vessels)
		{
			flightState.SubObjects.Add(vessel.ToSaveGameObject(crewRoster));
		}
	
		return SaveGameObject;
	}
	
	private void Prepare()
	{
		//get all crew as loaded. needed for vessels
		var crewList = 
			(from flightState in SaveGameObject.SubObjects where flightState.Name == "FLIGHTSTATE"
			from crew in flightState.SubObjects where crew.Name == "CREW"
			select new Crew(crew))
			.ToList();
			
		//get all vessels
		Vessels = 
			(from flightState in SaveGameObject.SubObjects where flightState.Name == "FLIGHTSTATE"
			from vessel in flightState.SubObjects where vessel.Name == "VESSEL"
			select new Vessel(crewList, vessel))
			.ToList();
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