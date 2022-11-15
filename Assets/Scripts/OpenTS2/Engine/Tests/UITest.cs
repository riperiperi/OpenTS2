﻿/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/. 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using OpenTS2.Content;
using OpenTS2.Common;
using OpenTS2.Engine.Core;
using OpenTS2.Content.DBPF;

namespace OpenTS2.Engine.Tests
{
    public class UITest : MonoBehaviour
    {
        public RawImage Image;
        public string PackageToLoad = "%UserDataDirectory%/Neighborhoods/N007/Thumbnails/N007_FamilyThumbnails.package";

        void Start()
        {
            var contentManager = ContentManager.Get();
            contentManager.Provider.AddPackage(PackageToLoad);
            var texture = contentManager.Provider.GetAsset<TextureAsset>(new ResourceKey(0x00000001, "N007_FamilyThumbnails", 0x8C3CE95A));
            Image.texture = texture.Texture;
        }
    }
}
