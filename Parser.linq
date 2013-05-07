<Query Kind="Program">
  <NuGetReference>Sprache</NuGetReference>
  <Namespace>Sprache</Namespace>
</Query>

void Main()
{
	var filename = @"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\saves\default\persistent.sfs";

	var saveGameParser = SetupParser();

	var universe = saveGameParser.Parse(FiletoString(filename));
	
	universe.Dump("SaveGame in Memory");
	
	using (var file = new StreamWriter(filename + ".output"))
	{
		universe.WriteToStream(file);
	}
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
					select new Tuple<string, string>(i, v);
	
	Parser<SaveGameObject> section = null;
				
	section = from sectionName in identifier
				from _2 in beginBlock
				from parameters in parameter.Many().Select(x => x.ToList())
				from sections in Parse.Ref(() => section).Many().Select(x => x.ToList())
				from _3 in endBlock
				select new SaveGameObject() { Name = sectionName, Parameters = parameters, SubObjects = sections };
				
	return section;
}

public class SaveGameObject 
{
	public string Name { get; set; }
	public IList<Tuple<string, string>> Parameters { get; set; }
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
			writer.WriteLine("{0}{1} = {2}", subLevelPrefix, parameter.Item1, parameter.Item2);	
		}

		foreach (var subObject in SubObjects)
		{
			subObject.WriteToStream(writer, subLevel);
		}
		writer.WriteLine("{0}}}", linePrefix);	
	}
}

// Define other methods and classes here