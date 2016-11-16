using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace SBRosterSort
{
    class SaveLoad
    {
        public class FighterSerializeData
        {
            public Dictionary<string, SBSort.Record> Fighters;
            public Dictionary<string, SBSort.SpecificFight> SpecificFights;
        }

        public static void Save(FighterSerializeData Data)
        {
            //var Data = new { Fighters = Fighters, Fights = SpecificFights };

            string StringData = JsonConvert.SerializeObject(Data, Formatting.Indented);

            /*int LastIndex = 1;
            if(!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }
            else
            {
                string[] DataNames = Directory.GetFiles("Data");
                Array.Sort(DataNames);
                string LastNumber = DataNames[DataNames.Length - 1].Substring(10);
                LastNumber = LastNumber.Remove(LastNumber.IndexOf('.'));
                LastIndex = Convert.ToInt32(LastNumber);
                ++LastIndex;
            }*/

            //using(System.IO.StreamWriter Writer = new System.IO.StreamWriter("Data/Data_" + LastIndex + ".json"))
            using(StreamWriter Writer = new StreamWriter("SBData.json"))
            {
                Writer.Write(StringData);
            }
        }

        public static FighterSerializeData Load()
        {
            FighterSerializeData Data = new FighterSerializeData();
            
            using(StreamReader Reader = new StreamReader("SBData.json"))
            {
                string StringData = Reader.ReadToEnd();
                Data = JsonConvert.DeserializeObject<FighterSerializeData>(StringData);
            }

            return Data;
        }
    }
}
