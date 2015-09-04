using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;

namespace FindSweetSpot {
    class Program {

        public class Chunk {
            public int start { get; set; }
            public int end { get; set; }
            public ResidualDataInfo startinfo { get; set; }
            public ResidualDataInfo endinfo { get; set; }
            public double score { get; set; }

            public Chunk(int s, ResidualDataInfo si, double sc ,int e, ResidualDataInfo ei) {
                start = s;
                startinfo = si;
                score = sc;
                end = e;
                endinfo = ei;
            }
        }
        public class ResidualDataInfo {
            public double observed {get; set;}
            public double predicted {get; set;}
            public double condition {get; set;}

            public ResidualDataInfo(double o, double p, double c){
                observed = o;
                predicted = p;
                condition = c;
            }
        }
        public class NNConfig {

            [JsonProperty("NetworkName")]
            public string network { get; set; }

            [JsonProperty("NetworkStructure")]
            public string Structure { get; set; }

            [JsonProperty("NetworkType")]
            public string Type { get; set; }

            [JsonProperty("ActivationFunction")]
            public string ActivationFunction { get; set; }

            [JsonProperty("TrainingMethodUsed")]
            public string TrainingMethod { get; set; }

            [JsonProperty("FinalError")]
            public string Error { get; set; }

            [JsonProperty("FinalCorrelation")]
            public string R { get; set; }

            [JsonProperty("TrainingData")]
            public string Data { get; set; }

            [JsonProperty("Frequency")]
            public string Frequency { get; set; }

            [JsonProperty("TakeProfit")]
            public string TakeProfit { get; set; }

            [JsonProperty("StopLoss")]
            public string StopLoss { get; set; }

            [JsonProperty("PredictionTime")]
            public string PredictionTime { get; set; }

            [JsonProperty("Inputs")]
            public string[] inputs { get; set; }

            [JsonProperty("Outputs")]
            public string[] outputs { get; set; }

            [JsonProperty("Normalization")]
            public string Normalization { get; set; }

            [JsonProperty("InputRule")]
            public string[][] InputRules { get; set; }

            [JsonProperty("OutputRule")]
            public string[][] OutputRules { get; set; }

            [JsonProperty("SweetSpot")]
            public string[] SweetSpot { get; set; }

        }

      
        [STAThread]
        static void Main(string[] args) {
            // Read the Residual Data and load into memory filter only the espected profit greater than 0.005
            string path = "";
            var dialog = new FolderBrowserDialog();
            using(dialog) {
                if(dialog.ShowDialog() == DialogResult.OK) {
                    path = dialog.SelectedPath;
                }
            }
            //string[] files = Directory.GetFiles(path);

            //string fileName = "test.txt";
            //string destPath =  $"{path}/Test/";
            //string destFile = Path.Combine(destPath, fileName);
            //// Copy the files and overwrite destination files if they already exist.
            //foreach(string s in files) {
            //    // Use static Path methods to extract only the file name from the path.
            //    fileName = Path.GetFileName(s);
            //    destFile = Path.Combine(destPath, fileName);
            //    File.Copy(s, destFile, true);
            //}

            // System.IO.File.Copy(sourceFile, destFile, true);

            calculateSweetspot(path);


            Console.WriteLine("Press any key to exit");
            Console.ReadLine();
            return;
        }
        public static void calculateSweetspot(string path) {
            List<ResidualDataInfo> ResidualData = new List<ResidualDataInfo>();
            List<Chunk> Chunks = new List<Chunk>();
            List<string> ResidualDataFiles = new List<string>();
            List<string> NNFiles = new List<string>();
            List<string> ConfigFiles = new List<string>();
            string[] filePaths = Directory.GetFiles(path);

            ResidualDataFiles = filePaths.Where(x => x.Contains("ResidualData.csv")).ToList();
            NNFiles = filePaths.Where(x => x.Contains("NN")).ToList();
            ConfigFiles = filePaths.Where(x => x.Contains("Config")).ToList();

            foreach(string StockResidualData in ResidualDataFiles) {
                ResidualData = new List<ResidualDataInfo>();
                if(File.Exists(StockResidualData)) {
                    StreamReader file = new StreamReader(StockResidualData);
                    string line = file.ReadLine(); // read header
                    while((line = file.ReadLine()) != null) {
                        string[] values = line.Split(',');
                        double observed = Convert.ToDouble(values[3]);
                        double predicted = Convert.ToDouble(values[4]);
                        if(predicted > 0.01) ResidualData.Add(new ResidualDataInfo(observed, predicted, ((predicted - observed) > 0.3 * predicted) ? 0 : predicted)); // 30% deviation
                    }
                    file.Close();
                }
                else {
                    Console.WriteLine("Error, could not find the file");
                    Console.WriteLine("Press any key to exit");
                    Console.ReadLine();
                    return;
                }
                if(ResidualData.Count == 0) { continue; }

                // Residual Data = [ observed, predicted, predicted if condition is met ]
                int batch_size = 50;
                Chunks = new List<Chunk>();
                List<ResidualDataInfo> SortedData = ResidualData.OrderByDescending(x => x.predicted).ToList();
                if(batch_size > SortedData.Count) batch_size = SortedData.Count;
                for(int pivot = 0; pivot + batch_size <= SortedData.Count; pivot += batch_size) {
                    double zerocounter = 0;
                    double x = pivot;
                    double mean = 0;
                    do {
                        zerocounter += SortedData.Skip(Convert.ToInt32(x)).Take(batch_size).Where(tick => tick.condition == 0).Count();
                        mean = ((mean * (x - pivot)) + (batch_size * SortedData.Skip(Convert.ToInt32(x)).Take(batch_size).Select(tick => tick.predicted).Average())) / (x + batch_size);
                        if(x + batch_size > SortedData.Count) x = SortedData.Count;
                        else x += batch_size;

                    } while((zerocounter / (x - pivot)) <= 0.05 && x < SortedData.Count);    // 98%
                    double score = (x - pivot) * mean; // Lenght of the chunck times the mean of the predicted profit
                    Chunks.Add(new Chunk((int)pivot, new ResidualDataInfo(SortedData[pivot].observed, SortedData[pivot].predicted, SortedData[pivot].condition), score, (int)x, new ResidualDataInfo(SortedData[(int)x - 1].observed, SortedData[(int)x - 1].predicted, SortedData[(int)x - 1].condition)));
                    if(pivot + batch_size > SortedData.Count) batch_size = SortedData.Count - pivot;

                }
                // Biggest Chunk
                Chunk SweetSpot = Chunks.OrderByDescending(x => x.score).First();
                Console.WriteLine("The sweet spot is between [{0} {1}]  [{3} {4}]for file {2} ", SweetSpot.start, SweetSpot.end, StockResidualData, SweetSpot.startinfo.predicted, SweetSpot.endinfo.predicted);

                string StockName = StockResidualData.Replace("ResidualData.csv", "");
                StockName = StockName.TrimEnd(new char[] { '4', '1', '2', '3' });

                string oldconfig = filePaths.Where(x => x.Contains("Config") && x.Contains(StockName)).First();
                NNConfig Config = JsonConvert.DeserializeObject<NNConfig>(File.ReadAllText(oldconfig));
                Config.SweetSpot = new string[] { SweetSpot.startinfo.predicted.ToString(), SweetSpot.endinfo.predicted.ToString() };

                //string Filepath = string.Format("{0}\\wSweetSpot\\{1}wSweetSpot", path, Config.network);

                string FileName = string.Format("{0}wSweetSpot", Config.network);
                string Filepath = string.Format("{0}/{1}", path, FileName);

                //if(!System.IO.Directory.Exists(Directory.GetParent(Filepath).ToString())) {
                //    System.IO.Directory.CreateDirectory(Filepath);
                //}
                using(StreamWriter file = new StreamWriter(Filepath)) {
                    file.Write(JsonConvert.SerializeObject(Config));
                }
            }
        }
    }
}
