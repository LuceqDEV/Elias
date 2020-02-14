﻿using Elias.Utilities;
using System;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Xml;
using System.Collections.Generic;

namespace EliasLibrary
{
    public class EliasAsset
    {
        private Elias elias;
        private XmlNode node;

        public bool IsIcon;
        public bool IsShadow;
        public bool IsFlipped;
        public bool IsMemberAlias;

        public string FlashAssetName;
        public string ShockwaveAssetName;

        public string FlashSourceAliasName;
        public string ShockwaveSourceAliasName;

        public int[] FlashRectanglePoint;
        public int[] ShockwaveRectanglePoint;

        public EliasAsset(Elias elias, XmlNode node)
        {
            this.elias = elias;
            this.node = node;
            this.FlashRectanglePoint = new int[2];
            this.ShockwaveRectanglePoint = new int[2];

            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var attribute = node.Attributes.Item(i);

                if (attribute == null)
                {
                    continue;
                }

                if (attribute.InnerText.Contains("_icon_"))
                {
                    IsIcon = true;
                    break;
                }

                if (attribute.InnerText.Contains("_sd_"))
                {
                    IsShadow = true;
                    break;
                }
            }

            this.ParseAssetNames();
            this.ParseRecPointNames();
        }

        public void ParseAssetNames()
        {
            if (IsIcon)
                return;

            if (IsShadow)
                return;

            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var attribute = node.Attributes.Item(i);

                if (attribute == null)
                {
                    continue;
                }

                if (attribute.Name == "name")
                {
                    this.FlashAssetName = attribute.InnerText;
                    this.ShockwaveAssetName = AssetUtil.ConvertFlashName(this.elias, attribute.InnerText, elias.X, elias.Y);
                }

                if (attribute.Name == "source")
                {
                    this.IsFlipped = true;
                    this.IsMemberAlias = true;
                    this.FlashSourceAliasName = attribute.InnerText;
                    this.ShockwaveSourceAliasName = AssetUtil.ConvertFlashName(this.elias, attribute.InnerText, elias.X, elias.Y);
                }
            }
        }

        public void ParseRecPointNames()
        {
            if (IsIcon)
                return;

            if (IsShadow)
                return;

            int x = -1;
            int y = -1;

            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var attribute = node.Attributes.Item(i);

                if (attribute == null)
                {
                    continue;
                }

                if (attribute.Name == "x")
                    x = int.Parse(attribute.InnerText);

                if (attribute.Name == "y")
                    y = int.Parse(attribute.InnerText);
            }

            if (x == -1 || y == -1)
            {
                this.FlashRectanglePoint = new int[] { 0, 0 };
                this.ShockwaveRectanglePoint = new int[] { 0, 0 };
                return;
            }

            this.FlashRectanglePoint = new int[] { x, y };
            this.ShockwaveRectanglePoint = new int[] { x - 32, y };
        }

        public void WriteAssets()
        {
            if (IsIcon)
                return;

            if (IsShadow)
                return;

            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var attribute = node.Attributes.Item(i);

                if (attribute == null)
                {
                    continue;
                }

                if (attribute.Name == "name")
                {
                    this.FlashAssetName = attribute.InnerText;
                    this.ShockwaveAssetName = AssetUtil.ConvertFlashName(this.elias, attribute.InnerText, elias.X, elias.Y);

                    var flashFile = ImageAssetUtil.SolveFile(elias.OUTPUT_PATH, FlashAssetName);

                    if (flashFile == null)
                    {
                        var symbolData = ImageAssetUtil.SolveSymbolReference(elias, FlashAssetName);

                        if (symbolData != null)
                        {
                            var symbolID = symbolData.Item1;
                            var symbolReference = symbolData.Item2;
                            var symbolFileName = Path.GetFileNameWithoutExtension(Path.GetFileName(symbolReference).Replace(symbolID + "_" + elias.Sprite + "_", ""));

                            var symbolAsset = elias.Assets.FirstOrDefault(asset => asset.FlashAssetName == symbolFileName && this.FlashRectanglePoint[0] == asset.FlashRectanglePoint[0]
                                && this.FlashRectanglePoint[1] == asset.FlashRectanglePoint[1]);

                            if (symbolAsset != null)
                            {
                                Console.WriteLine("Cloned: " + symbolFileName + " => " + FlashAssetName);

                                this.IsMemberAlias = true;
                                this.IsFlipped = false;
                                this.FlashSourceAliasName = symbolFileName;
                                this.ShockwaveSourceAliasName = AssetUtil.ConvertFlashName(this.elias, this.FlashSourceAliasName, elias.X, elias.Y);
                            }
                            else
                            {
                                // Copy it over because different regpoints
                                Console.WriteLine("Copied: " + symbolFileName + " => " + FlashAssetName);
                                File.Copy(symbolReference, Path.Combine(elias.OUTPUT_PATH, "images", FlashAssetName + ".png"));
                            }
                        }
                        else
                        {
                            if (!IsMemberAlias)
                            {
                                Console.WriteLine("Create blank sprite for: " + FlashAssetName);

                                Bitmap bmp = new Bitmap(1, 1);
                                bmp.Save(Path.Combine(elias.OUTPUT_PATH, "images", FlashAssetName + ".png"), ImageFormat.Png);
                            }
                        }
                    }
                }
            }
        }

        public void WriteImageNames()
        {
            if (IsIcon)
                return;

            if (IsShadow)
                return;

            if (!string.IsNullOrEmpty(FlashSourceAliasName))
                return;

            var sourceImage = ImageAssetUtil.SolveFile(elias.OUTPUT_PATH, FlashAssetName);
            
            File.Copy(sourceImage, Path.Combine(elias.IMAGE_PATH, this.ShockwaveAssetName + ".png"));
        }

        public void WriteRegPointData()
        {
            if (IsIcon)
                return;

            if (IsShadow)
                return;


            if (!string.IsNullOrEmpty(FlashSourceAliasName))
                return;

            File.WriteAllText(Path.Combine(elias.IMAGE_PATH, this.ShockwaveAssetName + ".txt"), ShockwaveRectanglePoint[0] + "," + ShockwaveRectanglePoint[1]);
        }
    }
}