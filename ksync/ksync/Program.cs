using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sprache;
using System.IO;

namespace ksync
{
    class Program
    {
        static string localSaveGameFolder = @".";
        static string userSpecificFolder = Path.Combine(localSaveGameFolder, "userdata");
        static string currentUserfile = Path.Combine(localSaveGameFolder, ".ksync-user");
        static string fileToSync = "quicksave.sfs"; //also try quickssave

        static void Main(string[] args)
        {
            if (args == null || args.Length < 1)
                throw new ArgumentException("no arguments");

            //parameters



            var command = args[0];

            if (command == null || command.Length < 1)
                throw new ArgumentException("no command specified");

            ExecuteCommand(command, args);



        }

        static void ExecuteCommand(string command, string[] args) //remember, first argument is used already.
        {
            switch (command.ToLower())
            {
                case "set-user":
                    {
                        SetUser(args);
                        break;
                    }
                case "pre":
                    {
                        Pre(args);
                        break;
                    }
                case "post":
                    {
                        Post(args);
                        break;
                    }
                default:
                    throw new ArgumentException("unknown command");
            }
        }

        static void SetUser(string[] args)
        {
            string username = args[1];

            if (username == null || username.Length < 1)
                throw new ArgumentException("no username specified");

            File.WriteAllText(currentUserfile, username);
        }

        static void Pre(string[] args)
        {
            string currentUser = GetUser();
            string userSpecificFile = Path.Combine(userSpecificFolder, currentUser + ".sfs");

            //setup
            if (!Directory.Exists(userSpecificFolder)) Directory.CreateDirectory(userSpecificFolder);
            var fileConverter = new KspFileReader(); //loads and saves the format that sfs is. can read other file types, like .craft

            //load up my saved game
            var saveGameData = fileConverter.ReadFile(Path.Combine(localSaveGameFolder, fileToSync)); //get data
            var myUniverse = new Universe(saveGameData); //convert to "smart data" (understands the contents a bit)

            //clean it
            myUniverse.RemoveVessels(v => !v.IsOwnedBy(currentUser));

            //resave as the "clean" version under my usename
            fileConverter.SaveFile(myUniverse.ToSaveGameObject(), userSpecificFile);
        }


        static void Post(string[] args)
        {
            string currentUser = GetUser();
            string userSpecificFile = Path.Combine(userSpecificFolder, currentUser + ".sfs");
            var fileConverter = new KspFileReader();

            //use current users's as the starting point for merge.
            var myUniverse = new Universe(fileConverter.ReadFile(userSpecificFile));

            foreach (var file in Directory.GetFiles(userSpecificFolder, "*.sfs"))
            {
                if (!Path.GetFileName(userSpecificFile).Equals(Path.GetFileName(file)))
                {
                    var otherUniverse = new Universe(fileConverter.ReadFile(file));

                    myUniverse.ImportVessels(otherUniverse);
                }
            }

            fileConverter.SaveFile(myUniverse.ToSaveGameObject(), fileToSync);
        }

        static string GetUser()
        {
            return File.ReadAllText(currentUserfile);
        }
    }

    public class KspFileReader
    {
        Parser<SaveGameObject> parser;

        public KspFileReader()
        {
            var newlineChars = Environment.NewLine.ToCharArray();
            var identifierChars = new char[] { '_' };

            var identifier = Parse.Char(c => char.IsLetterOrDigit(c) || identifierChars.Contains(c), "identifier character").Many().Token().Text();

            var separator = Parse.String("= ");
            var newline = Parse.String(Environment.NewLine).Text();
            var value = Parse.Char(c => !newlineChars.Contains(c), "non-newline").Many().Text();
            var beginBlock = Parse.Char('{').Token();
            var endBlock = Parse.Char('}').Token();

            var parameter = from i in identifier
                            from _ in separator
                            from v in value
                            from _2 in newline
                            select new SaveGameParameter() { Name = i, Value = v };

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

            var flightState = (from fs in SaveGameObject.SubObjects
                                where fs.Name == "FLIGHTSTATE"
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

            var activeVesselParameter = flightState.Parameters.Where(p => p.Name == "activeVessel").Single();

            if (Vessels.Contains(ActiveVessel))
                activeVesselParameter.Value = Vessels.IndexOf(ActiveVessel).ToString();
            else
                activeVesselParameter.Value = "0";

            //flightState.Parameters.Where(p => p.Name == "activeVessel").Single().Value

            return SaveGameObject;
        }

        private void Prepare()
        {
            var flightState = (from fs in SaveGameObject.SubObjects where fs.Name == "FLIGHTSTATE" select fs).Single();

            //get all crew as loaded. needed for vessels
            var crewList =
                (from crew in flightState.SubObjects
                    where crew.Name == "CREW"
                    select new Crew(crew))
                .ToList();

            //get all vessels
            Vessels =
                (from vessel in flightState.SubObjects
                    where vessel.Name == "VESSEL"
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
            (from part in o.SubObjects
                where part.Name == "PART"
                from p in part.Parameters
                where p.Name == "crew"
                select crewList[int.Parse(p.Value)]).ToList().ForEach(c => Crew.Add(c));

        }

        public bool IsOwnedBy(string name)
        {
            return o.Parameters.Where(p => p.Name == "name").Single().Value.ToUpper().StartsWith(name.ToUpper() + ":");
        }

        public SaveGameObject ToSaveGameObject(IList<Crew> crewList)
        {
            var crewParameters = (from part in o.SubObjects
                                    where part.Name == "PART"
                                    from p in part.Parameters
                                    where p.Name == "crew"
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
}
