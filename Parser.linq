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
					select new KeyValuePair<string, string>(i, v);
	
	
	Parser<SaveGameObject> sectionContents = null;
	
	Parser<SaveGameObject> section = null;
				
	section = from sectionName in identifier
				from _2 in beginBlock
				from parameters in parameter.Many().Select(x => x.ToList())
				from sections in Parse.Ref(() => section).Many().Select(x => x.ToList())
				from _3 in endBlock
				select new SaveGameObject() { Name = sectionName, Parameters = parameters, SubObjects = sections };
				
	var sfsFile = section;
					
					
	
					
	//var parameterOrBlock = parameter.Or(block);
	
	parameter.Many().Parse(@"attached = True
	attached = True
	
	attached = True
	tmode = 0
	attached = True
	attached = True
	tmode = 0
	trans_spd_act = 0
	").Dump();
	
	
	sfsFile.Parse(@"PART
			{
				attached = True
				tmode = 0, INT
				trans_spd_act = 0, FLOAT
				trans_kill_h = False, BOOL
				trans_land = False, BOOL
				
			}").Dump();
	
	sfsFile.Parse(fileContents).Dump();
	
	
	//fileContents.Dump();
}

public class SaveGameObject 
{
	public string Name { get; set; }
	public IList<KeyValuePair<string, string>> Parameters { get; set; }
	public IList<SaveGameObject> SubObjects { get; set; }
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