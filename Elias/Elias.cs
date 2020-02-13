﻿using Elias.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EliasLibrary
{
    public class Elias
    {
        public string Sprite;
        public bool IsSmallFurni;
        public int X;
        public int Y;

        public string FullFileName;
        public string FileDirectory;

        public string FFDEC_PATH;
        public string OUTPUT_PATH;
        public string DIRECTOR_PATH;

        public string CAST_PATH
        {
            get { return Path.Combine(OUTPUT_PATH, "cast_data"); }
        }

        public string IMAGE_PATH
        {
            get { return Path.Combine(CAST_PATH, "images"); }
        }

        private List<EliasAsset> Assets;

        public Elias(string sprite, bool IsSmallFurni, string fileName, int X, int Y, string FFDEC_PATH, string OUTPUT_PATH, string DIRECTOR_PATH)
        {
            this.Sprite = sprite;
            this.IsSmallFurni = IsSmallFurni;
            this.FullFileName = fileName;
            this.X = X;
            this.Y = Y;
            this.FileDirectory = new FileInfo(this.FullFileName).DirectoryName;
            this.FFDEC_PATH = FFDEC_PATH;
            this.OUTPUT_PATH = OUTPUT_PATH;
            this.DIRECTOR_PATH = DIRECTOR_PATH;
            this.Assets = new List<EliasAsset>();
        }

        public void Parse()
        {
            this.TryCleanup();
            this.ExtractAssets();
            this.GenerateAliases();
            this.CreateMemberalias();
            this.GenerateProps();
            this.GenerateAssetIndex();
            this.GenerateAnimations();
            this.RunEliasDirector();
        }

        private void TryCleanup()
        {
            try
            {
                if (Directory.Exists(this.OUTPUT_PATH))
                    Directory.Delete(this.OUTPUT_PATH, true);

                Directory.CreateDirectory(this.OUTPUT_PATH);

                if (Directory.Exists(this.CAST_PATH))
                    Directory.Delete(this.CAST_PATH, true);

                Directory.CreateDirectory(this.CAST_PATH);

                if (Directory.Exists(this.IMAGE_PATH))
                    Directory.Delete(this.IMAGE_PATH, true);

                Directory.CreateDirectory(this.IMAGE_PATH);
            }
            catch
            {

            }
            finally
            {
                File.WriteAllText(Path.Combine(CAST_PATH, "sprite.data"),
                    string.Format("{0}|{1}", this.Sprite, (this.IsSmallFurni ? "small" : "large")));
            }
        }

        private void ExtractAssets()
        {
            var p = new Process();
            p.StartInfo.FileName = "java";
            p.StartInfo.Arguments = string.Format("-jar \"" + FFDEC_PATH + "\" -export \"binaryData,image\" \"{0}\" \"{1}\"", OUTPUT_PATH, this.FullFileName);
            p.Start();
            p.WaitForExit();
        }

        private void RunEliasDirector()
        {
            try
            {
                //Directory.Delete(Path.Combine(OUTPUT_PATH, "images"), true);
                //Directory.Delete(Path.Combine(OUTPUT_PATH, "binaryData"), true);
            }
            catch { }

            var p = new Process();
            p.StartInfo.WorkingDirectory = new FileInfo(DIRECTOR_PATH).DirectoryName;
            p.StartInfo.FileName = DIRECTOR_PATH;
            p.Start();
            p.WaitForExit();
        }

        private void GenerateAliases()
        {
            var xmlData = BinaryDataUtil.SolveFile(this.OUTPUT_PATH, "assets");

            if (xmlData == null)
            {
                return;
            }

            var assets = xmlData.SelectSingleNode("//assets");

            for (int i = 0; i < assets.ChildNodes.Count; i++)
            {
                var node = assets.ChildNodes.Item(i);

                if (node == null)
                {
                    continue;
                }

                if (IsSmallFurni && node.OuterXml.Contains("_64_"))
                {
                    continue;
                }

                if (!IsSmallFurni && node.OuterXml.Contains("_32_"))
                {
                    continue;
                }

                var eliasAlias = new EliasAsset(this, node);

                eliasAlias.ParseAssetNames();

                if (eliasAlias.ShockwaveAssetName == null)
                {
                    continue;
                }

                eliasAlias.WriteImageNames();
                eliasAlias.ParseRecPointNames();
                eliasAlias.WriteRegPointData();

                Assets.Add(eliasAlias);
            }
        }

        private void CreateMemberalias()
        {
            StringBuilder stringBuilder = new StringBuilder();

            foreach (var eliasAsset in Assets)
            {
                if (eliasAsset.IsShadow)
                    continue;

                if (eliasAsset.IsIcon)
                    continue;

                if (eliasAsset.ShockwaveSourceAliasName != null)
                {
                    stringBuilder.Append(eliasAsset.ShockwaveAssetName);
                    stringBuilder.Append("=");
                    stringBuilder.Append(eliasAsset.ShockwaveSourceAliasName);
                    stringBuilder.Append("*");
                    stringBuilder.Append("\r");
                }
            }

            File.WriteAllText(Path.Combine(CAST_PATH, "memberalias.index"), stringBuilder.ToString());
        }

        private void GenerateProps()
        {
            char[] alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToLower().ToCharArray();
            var xmlData = BinaryDataUtil.SolveFile(this.OUTPUT_PATH, "visualization");

            List<string> sections = new List<string>();

            if (xmlData == null)
            {
                return;
            }

            var visualisation = xmlData.SelectSingleNode("//visualizationData/graphics/visualization/layers");

            for (int i = 0; i < visualisation.ChildNodes.Count; i++)
            {
                var node = visualisation.ChildNodes.Item(i);

                if (node == null)
                {
                    continue;
                }

                if (node.Name != "layer")
                {
                    continue;
                }

                char letter = alphabet[int.Parse(node.Attributes.GetNamedItem("id").InnerText)];

                string firstSection = "[\"" + letter + "\": [{0}]]";
                string secondSection = "";

                if (node.Attributes.GetNamedItem("z") != null)
                {
                    secondSection += "#zshift: [" + node.Attributes.GetNamedItem("z").InnerText + "], ";
                }

                if (node.Attributes.GetNamedItem("alpha") != null)
                {
                    double alphaValue = double.Parse(node.Attributes.GetNamedItem("alpha").InnerText);
                    double newValue = (double)((alphaValue / 255) * 100);
                    secondSection += "#ink: " + (int)newValue + ", ";
                }

                if (secondSection.Length > 0)
                {
                    secondSection = secondSection.TrimEnd(", ".ToCharArray());
                }
                else
                {
                    secondSection = ":";
                }

                sections.Add(string.Format(firstSection, secondSection));
            }

            File.WriteAllText(Path.Combine(CAST_PATH, ((this.IsSmallFurni ? "s_" : "") + this.Sprite) + ".props"), "[" + string.Join(", ", sections) + "]");
        }

        private void GenerateAnimations()
        {

            char[] alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToLower().ToCharArray();
            var xmlData = BinaryDataUtil.SolveFile(this.OUTPUT_PATH, "visualization");

            Dictionary<string, Dictionary<int, List<string>>> sections = new Dictionary<string, Dictionary<int, List<string>>>();

            if (xmlData == null)
            {
                return;
            }

            var totalStates = 0;
            var states = "";
            var visualisation = xmlData.SelectSingleNode("//visualizationData/graphics/visualization/animations");

            for (int i = 0; i < visualisation.ChildNodes.Count; i++)
            {
                var node = visualisation.ChildNodes.Item(i);

                if (node == null)
                {
                    continue;
                }

                int id = int.Parse(node.Attributes.GetNamedItem("id").InnerText);
                states += (id + 1) + ", ";
                
                if ((id + 1) > totalStates)
                {
                    totalStates = id + 1;
                }

                for (int j = 0; j < node.ChildNodes.Count; j++)
                {
                    var layer = node.ChildNodes.Item(j);

                    if (layer == null)
                    {
                        continue;
                    }

                    int layerId = int.Parse(layer.Attributes.GetNamedItem("id").InnerText);
                    var layerLetter = Convert.ToString(alphabet[layerId]);

                    for (int k = 0; k < layer.ChildNodes.Count; k++)
                    {
                        var frame = layer.ChildNodes.Item(k);

                        if (frame == null || frame.ChildNodes.Count == 0)
                        {
                            continue;
                        }

                        if (!sections.ContainsKey(layerLetter))
                        {
                            sections.Add(layerLetter, new Dictionary<int, List<string>>());
                        }


                        if (!sections[layerLetter].ContainsKey(id))
                        {
                            sections[layerLetter].Add(id, new List<string>());
                        }

                        sections[layerLetter][id].Add(frame.ChildNodes.Item(0).Attributes.GetNamedItem("id").InnerText);

                        //Console.WriteLine(frame.ChildNodes.Item(0).Attributes.GetNamedItem("id").InnerText);
                        //sections[layerLetter].Add(frame.ChildNodes.Item(0).Attributes.GetNamedItem("id").InnerText);
                    }
                }
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[\r");
            stringBuilder.Append("states:[" + states.TrimEnd(", ".ToCharArray()) + "],\r");
            stringBuilder.Append("layers:[\r");

            foreach (var animation in sections)
            {
                stringBuilder.Append(animation.Key + ": [ ");

                for (int i = 0; i < totalStates; i++)
                {
                    stringBuilder.Append("[ frames:[ ");
                    stringBuilder.Append(string.Join(",", animation.Value[i]));

                    if (totalStates > i + 1)
                    {
                        stringBuilder.Append(" ] ], ");
                    }
                    else
                    {
                        stringBuilder.Append(" ] ] ");
                    }
                  
                }

                stringBuilder.Append("]\r");
            }

            stringBuilder.Append("]\r");
            stringBuilder.Append("]\r");

            File.WriteAllText(Path.Combine(CAST_PATH, ((this.IsSmallFurni ? "s_" : "") + this.Sprite) + ".data"), stringBuilder.ToString());
        }

        private void GenerateAssetIndex()
        {
            // [#id: "s_tv_flat", #classes: ["Active Object Class",  "Active Object Extension Class"]]
            File.WriteAllText(Path.Combine(CAST_PATH, "asset.index"), 
                "[#id: \"" + ((this.IsSmallFurni ? "s_" : "") + this.Sprite) + "\", #classes: [\"Active Object Class\",  \"Active Object Extension Class\"]]");
        }


    }
}
