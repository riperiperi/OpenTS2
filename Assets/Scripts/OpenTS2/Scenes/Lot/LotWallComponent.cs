using OpenTS2.Common;
using OpenTS2.Components;
using OpenTS2.Content;
using OpenTS2.Content.DBPF;
using OpenTS2.Content.DBPF.Scenegraph;
using OpenTS2.Files.Formats.DBPF;
using OpenTS2.Scenes.Lot.Roof;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace OpenTS2.Scenes.Lot
{
    public static class WallType
    {
        public const int Normal = 1;
        public const int ThinFence = 2; // unused?
        public const int Roof = 3;
        public const int DeckInvis = 4;
        public const int Deck = 16;
        public const int Foundation = 23;
        public const int Deck2 = 24;
        public const int Deck3 = 26;
        public const int Pool = 29;
        public const int OFBWall = 300;
        public const int OFBScreen = 301;
    }

    public class LotWallComponent : AssetReferenceComponent
    {
        private const string ThicknessTexture = "wall_top";
        private const string FallbackMaterial = "wall_wallboard";
        private const float HalfWallHeight = 1f;
        private const float WallHeight = 3f;
        private const float Thickness = 0.075f;
        private const float RoofOffset = Thickness * 2.5f;

        private class FenceCollection
        {
            private CatalogFenceAsset _asset;
            private ContentProvider _contentProvider;
            private GameObject _parent;
            private bool _hasDiag;

            private ScenegraphResourceAsset _railCres;
            private ScenegraphResourceAsset _diagRailCres;
            private ScenegraphResourceAsset _postCres;

            private List<GameObject> _rails;
            private List<GameObject> _diagRails;
            private List<GameObject> _posts;

            public FenceCollection(ContentProvider contentProvider, GameObject parent, uint guid)
            {
                var catalog = CatalogManager.Get();

                _asset = catalog.GetFenceById(guid);

                if (_asset == null)
                {
                    return;
                }

                _hasDiag = _asset.DiagRail != null;
                _parent = parent;

                _railCres = contentProvider.GetAsset<ScenegraphResourceAsset>(
                    new ResourceKey(_asset.Rail + "_cres", GroupIDs.Scenegraph,
                        TypeIDs.SCENEGRAPH_CRES));

                _diagRailCres = _hasDiag ? contentProvider.GetAsset<ScenegraphResourceAsset>(
                    new ResourceKey(_asset.DiagRail + "_cres", GroupIDs.Scenegraph,
                        TypeIDs.SCENEGRAPH_CRES)) : null;

                _postCres = contentProvider.GetAsset<ScenegraphResourceAsset>(
                    new ResourceKey(_asset.Post + "_cres", GroupIDs.Scenegraph,
                        TypeIDs.SCENEGRAPH_CRES));

                _rails = new List<GameObject>();
                _diagRails = new List<GameObject>();
                _posts = new List<GameObject>();

                parent.GetComponent<LotWallComponent>().AddReference(_railCres, _diagRailCres, _postCres);
            }

            public void AddRail(float fromX, float fromY, float toX, float toY, float fromElevation, float toElevation)
            {
                float direction = Mathf.Rad2Deg * Mathf.Atan2(toY - fromY, toX - fromX);
                Vector3 shearVec = new Vector3(toX - fromX, toY - fromY, toElevation - fromElevation);
                float magnitude = new Vector2(toX - fromX, toY - fromY).magnitude;
                float shearMagnitude = shearVec.magnitude;

                GameObject model;

                if (magnitude > 1.1 && _diagRailCres != null)
                {
                    model = _diagRailCres.CreateRootGameObject();

                    model.transform.parent = _parent.transform;

                    magnitude /= Mathf.Sqrt(2);

                    _diagRails.Add(model);
                }
                else
                {
                    if (_railCres != null)
                    {
                        model = _railCres.CreateRootGameObject();

                        model.transform.parent = _parent.transform;

                        _rails.Add(model);
                    }
                    else
                    {
                        return;
                    }
                }

                Transform modelSpace = model.transform.GetChild(0);
                modelSpace.localScale = new Vector3(magnitude, 1, 1);

                if (shearVec.z != 0)
                {
                    // Shear transform
                    // Because Unity doesn't support submitting your own transform matrix for some reason,
                    // We need to combine a bunch of transformations to perform a shear.
                    // In this case, we want the Z dimension to be sheared with x and y left intact.

                    float realAngle = Mathf.Atan2(shearVec.z, magnitude);
                    float shearAngle = realAngle > 0 ? Mathf.PI / 2 - realAngle : Mathf.PI / -2 - realAngle;

                    var top = new GameObject("skew_top").transform;
                    top.SetParent(model.transform);
                    var mid = new GameObject("skew_mid").transform;
                    mid.SetParent(top);
                    var bot = new GameObject("skew_bot").transform;
                    bot.SetParent(mid);
                    modelSpace.SetParent(bot);
                    modelSpace.localRotation = Quaternion.identity;

                    top.localRotation = Quaternion.Euler(0, -realAngle * Mathf.Rad2Deg, 0);
                    mid.localRotation = Quaternion.Euler(0, 45, 0);
                    bot.localRotation = Quaternion.Euler(0, (-shearAngle / 2) * Mathf.Rad2Deg, 0);

                    float initialScale = shearMagnitude;
                    float finalScale = Mathf.Sqrt(2);

                    top.localScale = new Vector3(finalScale / Mathf.Sin(shearAngle), 1, finalScale);
                    mid.localScale = new Vector3(Mathf.Sin(shearAngle / 2), 1, Mathf.Cos(shearAngle / 2));
                    bot.localScale = new Vector3(initialScale, 1, 1 / initialScale);

                    modelSpace = top;
                }

                modelSpace.localPosition = new Vector3(fromX, fromY, fromElevation);
                modelSpace.localRotation = Quaternion.Euler(0, 0, direction) * modelSpace.localRotation;
            }

            public void AddPost(float x, float y, float elevation)
            {
                if (_postCres != null)
                {
                    var model = _postCres.CreateRootGameObject();

                    model.transform.parent = _parent.transform;

                    model.transform.GetChild(0).localPosition = new Vector3(x, y, elevation);

                    _posts.Add(model);
                }
            }

            public void Clear()
            {
                foreach (GameObject rail in _rails)
                {
                    Destroy(rail);
                }

                foreach (GameObject rail in _diagRails)
                {
                    Destroy(rail);
                }

                foreach (GameObject post in _posts)
                {
                    Destroy(post);
                }

                _rails.Clear();
                _posts.Clear();
            }
        }

        private struct WallIntersectionMember
        {
            public int WallID;
            public float LeftExtent;
            public float RightExtent;

            public WallIntersectionMember(int wallId)
            {
                WallID = wallId;
                LeftExtent = 0;
                RightExtent = 0;
            }
        }

        private class WallIntersection
        {
            public WallGraphPositionEntry Position;
            public List<WallIntersectionMember> IncomingLines;
            public bool Simple; // Wall intersection is flat or capped.

            public WallIntersection(WallGraphPositionEntry position)
            {
                Position = position;
                IncomingLines = new List<WallIntersectionMember>();
                Simple = true;
            }
        }

        private StringMapAsset _patternMap;
        private WallLayerAsset _wallLayer;
        private WallGraphAsset _wallGraph;
        private FencePostLayerAsset _fencePosts;
        private _3DArrayAsset<float> _elevationData;
        private RoofCollection _roofs;

        private PatternMesh _thickness;
        private PatternMesh[] _patterns;

        private Dictionary<int, WallIntersection> _intersections;
        private Dictionary<uint, FenceCollection> _fenceByGuid;

        public void CreateFromLotAssets(
            StringMapAsset patternMap,
            WallLayerAsset wallLayer,
            WallGraphAsset wallGraph,
            FencePostLayerAsset fencePosts,
            _3DArrayAsset<float> elevationData,
            RoofCollection roofs)
        {
            _patternMap = patternMap;
            _wallLayer = wallLayer;
            _wallGraph = wallGraph;
            _fencePosts = fencePosts;
            _elevationData = elevationData;
            _roofs = roofs;

            _fenceByGuid = new Dictionary<uint, FenceCollection>();

            if (wallGraph.Floors != elevationData.Depth)
            {
                throw new InvalidOperationException("Size mismatch between elevation and wall graph");
            }

            LoadPatterns();
            BuildWallIntersections();
            BuildWallMeshes();
            AddFencePosts();
        }

        private ScenegraphMaterialDefinitionAsset LoadMaterial(ContentProvider contentProvider, string name)
        {
            var material = contentProvider.GetAsset<ScenegraphMaterialDefinitionAsset>(new ResourceKey($"{name}_txmt", GroupIDs.Scenegraph, TypeIDs.SCENEGRAPH_TXMT));

            AddReference(material);

            return material;
        }

        private FenceCollection GetFence(uint guid)
        {
            if (!_fenceByGuid.TryGetValue(guid, out FenceCollection result))
            {
                result = new FenceCollection(ContentProvider.Get(), gameObject, guid);

                _fenceByGuid[guid] = result;
            }

            return result;
        }

        private void LoadPatterns()
        {
            // Load the patterns. Some references are by asset name (do not exist in catalog), others are by catalog GUID.

            var contentProvider = ContentProvider.Get();
            var catalogManager = CatalogManager.Get();

            ushort highestId = _patternMap.Map.Count == 0 ? (ushort)0 : _patternMap.Map.Keys.Max();
            _patterns = new PatternMesh[highestId + 1];

            foreach (StringMapEntry entry in _patternMap.Map.Values)
            {
                string materialName;
                if (uint.TryParse(entry.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint guid))
                {
                    var catalogEntry = catalogManager.GetEntryById(guid);

                    materialName = catalogEntry?.Material ?? FallbackMaterial;
                }
                else if (entry.Value == "blank")
                {
                    materialName = FallbackMaterial;
                }
                else
                {
                    materialName = entry.Value.StartsWith("wall_") ? (entry.Value + "_base") : ("wall_" + entry.Value + "_base");
                }

                // Try fetch the texture using the material name.

                var material = LoadMaterial(contentProvider, materialName);

                try
                {
                    _patterns[entry.Id] = material == null ? null : new PatternMesh(gameObject, materialName, material?.GetAsUnityMaterial());
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            _thickness = new PatternMesh(gameObject, ThicknessTexture, LoadMaterial(contentProvider, ThicknessTexture).GetAsUnityMaterial());
        }

        private bool IsWallThick(int type)
        {
            switch (type)
            {
                case WallType.Normal:
                case WallType.Roof:
                case WallType.Foundation:
                case WallType.Pool:
                case WallType.OFBWall:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsFence(int type)
        {
            return (uint)type > 255;
        }

        private static int CalculateElevationIndex(int height, int x, int y)
        {
            return x * height + y;
        }

        private static float GetElevationInt(float[] elevation, int width, int height, float x, float y)
        {
            return elevation[CalculateElevationIndex(height, (int)x, (int)y)];
        }

        private static float GetElevationIntUpper(float[] elevation, int width, int height, float x, float y, float previous)
        {
            return elevation == null ? previous + WallHeight : elevation[CalculateElevationIndex(height, (int)x, (int)y)];
        }

        private static float GetElevationInterp(float[] elevation, int width, int height, float x, float y)
        {
            int wm1 = width - 1;
            int hm1 = height - 1;

            float i0 = elevation[CalculateElevationIndex(height, (int)x, (int)y)];
            float i1 = elevation[CalculateElevationIndex(height, Math.Min(wm1, (int)x + 1), (int)y)];
            float j0 = elevation[CalculateElevationIndex(height, (int)x, Math.Min(hm1, (int)y + 1))];
            float j1 = elevation[CalculateElevationIndex(height, Math.Min(wm1, (int)x + 1), Math.Min(hm1, (int)y + 1))];

            float xi = x % 1;
            float yi = y % 1;

            return Mathf.Lerp(Mathf.Lerp(i0, i1, xi), Mathf.Lerp(j0, j1, xi), yi);
        }

        private static float GetElevationInterpUpper(float[] elevation, int width, int height, float x, float y, float previous)
        {
            return elevation == null ? previous + WallHeight : GetElevationInterp(elevation, width, height, x, y);
        }

        private WallIntersection GetOrAddIntersection(int id)
        {
            if (_intersections.TryGetValue(id, out WallIntersection intersection))
            {
                return intersection;
            }

            intersection = new WallIntersection(_wallGraph.Positions[id]);
            _intersections.Add(id, intersection);

            return intersection;
        }

        private void AddToIntersection(WallIntersection intersection, ref WallGraphLineEntry line, int lineIndex, bool isTo)
        {
            WallIntersectionMember newMember = new WallIntersectionMember(lineIndex);

            if (intersection.IncomingLines.Count > 0)
            {
                // Evaluate extents for new line, update others based on preference.

                ref WallGraphPositionEntry pos = ref intersection.Position;
                WallGraphPositionEntry inFrom = isTo ? _wallGraph.Positions[line.FromId] : _wallGraph.Positions[line.ToId]; //y

                Vector2 vecIntoIntersection = new Vector2(pos.XPos - inFrom.XPos, pos.YPos - inFrom.YPos);
                vecIntoIntersection.Normalize();

                int count = intersection.IncomingLines.Count;

                newMember.LeftExtent = float.PositiveInfinity;
                newMember.RightExtent = float.PositiveInfinity;

                for (int i = 0; i < count; i++)
                {
                    WallIntersectionMember otherMember = intersection.IncomingLines[i];

                    ref WallGraphLineEntry otherLine = ref _wallGraph.Lines[otherMember.WallID];
                    bool otherTo = otherLine.FromId == intersection.Position.Id;
                    WallGraphPositionEntry outTo = otherTo ?
                        _wallGraph.Positions[otherLine.ToId] :
                        _wallGraph.Positions[otherLine.FromId];

                    Vector2 vecOutIntersection = new Vector2(outTo.XPos - pos.XPos, outTo.YPos - pos.YPos);

                    vecOutIntersection.Normalize();

                    float dot = Vector2.Dot(vecIntoIntersection, vecOutIntersection);

                    if (Mathf.Approximately(dot, 1f) && count == 1)
                    {
                        // If the two lines are facing the same direction, the extents are still 0 and this is still the simple case.

                        newMember.LeftExtent = 0;
                        newMember.RightExtent = 0;

                        break;
                    }

                    float vecAngle = Mathf.Acos(dot) * Mathf.Sign(vecIntoIntersection.x * vecOutIntersection.y - vecIntoIntersection.y * vecOutIntersection.x);
                    float angle = (vecAngle) / 2;

                    float extent = Thickness * Mathf.Tan(angle);

                    // When incoming, the left side extends forwards by extent, right side extends forwards by -extent.
                    // When outgoing, it's the opposite, as an incoming line's left side is the right side of the outgoing version.
                    // Prefer the smallest extent between both lines in the intersection.

                    float newSign = isTo ? 1 : -1;
                    newMember.LeftExtent = Math.Min(extent * newSign, newMember.LeftExtent);
                    newMember.RightExtent = Math.Min(-extent * newSign, newMember.RightExtent);

                    if (count == 1)
                    {
                        // Prepare these for getting new extents. Only saved back in non-simple-cases.
                        otherMember.LeftExtent = float.PositiveInfinity;
                        otherMember.RightExtent = float.PositiveInfinity;
                    }

                    float otherSign = otherTo ? 1 : -1;

                    otherMember.LeftExtent = Math.Min(extent * otherSign, otherMember.LeftExtent);
                    otherMember.RightExtent = Math.Min(-extent * otherSign, otherMember.RightExtent);

                    // Save back new extents.
                    intersection.IncomingLines[i] = otherMember;
                }
            }

            intersection.IncomingLines.Add(newMember);
        }

        private void BuildWallIntersections()
        {
            WallGraphLineEntry[] lines = _wallGraph.Lines;
            _intersections = new Dictionary<int, WallIntersection>();

            for (int i = 0; i < lines.Length; i++)
            {
                ref WallGraphLineEntry line = ref lines[i];
                WallIntersection from = GetOrAddIntersection(line.FromId);
                WallIntersection to = GetOrAddIntersection(line.ToId);

                if (_wallLayer.Walls.TryGetValue(line.LayerId, out var layer) && IsWallThick(layer.WallType))
                {
                    // Add this line to both intersections
                    AddToIntersection(from, ref line, i, false);
                    AddToIntersection(to, ref line, i, true);
                }
            }
        }

        private void BuildWallMeshes()
        {
            // TODO: split pattern meshes by level?
            int width = _elevationData.Width;
            int height = _elevationData.Height;
            int floors = _elevationData.Depth;
            int baseFloor = _wallGraph.BaseFloor;

            WallGraphLineEntry[] lines = _wallGraph.Lines;
            Dictionary<int, WallGraphPositionEntry> positions = _wallGraph.Positions;
            Dictionary<int, WallLayerEntry> layer = _wallLayer.Walls;

            // Draw each line on the wall graph.

            int currentFloor = 0;
            float[] currentFloorElevation = _elevationData.Data[currentFloor];
            float[] nextFloorElevation = _elevationData.Data[currentFloor + 1];

            Vector3[] wallVertices = new Vector3[6];
            Vector3[] wallVertices2 = new Vector3[6];
            Vector3[] endVertices = new Vector3[4];
            Vector2[] endUVs = new Vector2[4];
            Vector2[] wallUVs = new Vector2[6];

            Vector3[] thicknessVerts = new Vector3[8];
            Vector2[] thicknessUVs = new Vector2[8];

            var thicknessComp = _thickness.Component;

            for (int i = 0; i < lines.Length; i++)
            {
                ref WallGraphLineEntry line = ref lines[i];

                WallIntersection fromI = _intersections[line.FromId];
                WallIntersection toI = _intersections[line.ToId];

                ref WallGraphPositionEntry from = ref fromI.Position;
                ref WallGraphPositionEntry to = ref toI.Position;

                int floor = from.Level - baseFloor;

                if (floor != currentFloor)
                {
                    currentFloor = floor;

                    currentFloorElevation = _elevationData.Data[floor];
                    nextFloorElevation = floor + 1 < floors ? _elevationData.Data[floor + 1] : null;
                }

                if (!layer.TryGetValue(line.LayerId, out WallLayerEntry layerElem))
                {
                    layerElem = new WallLayerEntry()
                    {
                        Id = line.LayerId,
                        Pattern1 = 65535,
                        Pattern2 = 65535,
                        WallType = 2
                    };

                    // This shouldn't happen...
                }
                
                if (IsFence(layerElem.WallType))
                {
                    var fence = GetFence((uint)layerElem.WallType);

                    fence.AddRail(
                        from.XPos,
                        from.YPos,
                        to.XPos,
                        to.YPos,
                        GetElevationInt(currentFloorElevation, width, height, from.XPos, from.YPos),
                        GetElevationInt(currentFloorElevation, width, height, to.XPos, to.YPos));

                    continue;
                }

                LotFloorPatternComponent lPattern = (layerElem.Pattern1 == 65535 ? null : _patterns[layerElem.Pattern1]?.Component);// ?? _thickness?.Component;
                LotFloorPatternComponent rPattern = (layerElem.Pattern2 == 65535 ? null : _patterns[layerElem.Pattern2]?.Component);// ?? _thickness?.Component;

                float midX = (from.XPos + to.XPos) / 2;
                float midY = (from.YPos + to.YPos) / 2;

                float floorFrom = GetElevationInt(currentFloorElevation, width, height, from.XPos, from.YPos);
                float floorMid = GetElevationInterp(currentFloorElevation, width, height, midX, midY);
                float floorTo = GetElevationInt(currentFloorElevation, width, height, to.XPos, to.YPos);

                wallVertices[0] = new Vector3(from.XPos, floorFrom, from.YPos);
                wallVertices[1] = new Vector3(midX, floorMid, midY);
                wallVertices[2] = new Vector3(to.XPos, floorTo, to.YPos);

                float heightTo = GetElevationIntUpper(nextFloorElevation, width, height, to.XPos, to.YPos, wallVertices[2].y);
                float heightMid = GetElevationInterpUpper(nextFloorElevation, width, height, midX, midY, wallVertices[1].y);
                float heightFrom = GetElevationIntUpper(nextFloorElevation, width, height, from.XPos, from.YPos, wallVertices[0].y);

                bool roofWall = layerElem.WallType == WallType.Roof;

                if (roofWall)
                {
                    heightTo = Mathf.Max(_roofs.GetHeightAt(to.XPos, to.YPos, heightTo) - RoofOffset, floorTo);
                    heightMid = Mathf.Max(_roofs.GetHeightAt(midX, midY, heightTo) - RoofOffset, floorMid);
                    heightFrom = Mathf.Max(_roofs.GetHeightAt(from.XPos, from.YPos, heightTo) - RoofOffset, floorFrom);
                }

                wallVertices[3] = new Vector3(to.XPos, heightTo, to.YPos);
                wallVertices[4] = new Vector3(midX, heightMid, midY);
                wallVertices[5] = new Vector3(from.XPos, heightFrom, from.YPos);

                float startUV = (wallVertices[5].y - wallVertices[0].y) / WallHeight;
                float midUV = (wallVertices[4].y - wallVertices[1].y) / WallHeight;
                float endUV = (wallVertices[3].y - wallVertices[2].y) / WallHeight;

                wallUVs[0] = new Vector2(0, roofWall ? 1 : startUV);
                wallUVs[1] = new Vector2(0.5f, roofWall ? 1 : midUV);
                wallUVs[2] = new Vector2(1, roofWall ? 1 : endUV);
                wallUVs[3] = new Vector2(1, roofWall ? (1 - endUV) : 0);
                wallUVs[4] = new Vector2(0.5f, roofWall ? (1 - midUV) : 0);
                wallUVs[5] = new Vector2(0, roofWall ? (1 - startUV) : 0);

                bool isThick = IsWallThick(layerElem.WallType);

                if (isThick)
                {
                    // Thick wall. Offset vertices and evaluate 

                    var wallVec = new Vector2(to.XPos - from.XPos, to.YPos - from.YPos);

                    wallVec.Normalize();
                    var thickVec = wallVec * Thickness;

                    var offsetR = new Vector3(-thickVec.y, 0, thickVec.x);
                    var offsetL = new Vector3(thickVec.y, 0, -thickVec.x);

                    WallIntersectionMember fromMember = fromI.IncomingLines.First(x => x.WallID == i);
                    WallIntersectionMember toMember = toI.IncomingLines.First(x => x.WallID == i);

                    var offsetLStart = offsetL + new Vector3(wallVec.x * fromMember.LeftExtent * -1, 0, wallVec.y * fromMember.LeftExtent * -1);
                    var offsetRStart = offsetR + new Vector3(wallVec.x * fromMember.RightExtent * -1, 0, wallVec.y * fromMember.RightExtent * -1);

                    var offsetLEnd = offsetL + new Vector3(wallVec.x * toMember.LeftExtent, 0, wallVec.y * toMember.LeftExtent);
                    var offsetREnd = offsetR + new Vector3(wallVec.x * toMember.RightExtent, 0, wallVec.y * toMember.RightExtent);

                    thicknessVerts[6] = wallVertices[5]; // Center Start
                    thicknessVerts[7] = wallVertices[3]; // Center End
                    
                    wallVertices2[0] = wallVertices[0] + offsetRStart;
                    wallVertices2[1] = wallVertices[1] + offsetR;
                    wallVertices2[2] = wallVertices[2] + offsetREnd;
                    wallVertices2[3] = wallVertices[3] + offsetREnd;
                    wallVertices2[4] = wallVertices[4] + offsetR;
                    wallVertices2[5] = wallVertices[5] + offsetRStart;

                    wallVertices[0] += offsetLStart;
                    wallVertices[1] += offsetL;
                    wallVertices[2] += offsetLEnd;
                    wallVertices[3] += offsetLEnd;
                    wallVertices[4] += offsetL;
                    wallVertices[5] += offsetLStart;

                    // Add wall thickness
                    
                    thicknessVerts[0] = wallVertices[5]; // L top start
                    thicknessVerts[1] = wallVertices[4]; // L top mid
                    thicknessVerts[2] = wallVertices[3]; // L top end
                    thicknessVerts[3] = wallVertices2[3]; // R top end
                    thicknessVerts[4] = wallVertices2[4]; // R top mid
                    thicknessVerts[5] = wallVertices2[5]; // R top start

                    for (int j = 0; j < 8; j++)
                    {
                        thicknessUVs[j] = new Vector2(0, thicknessVerts[j].z);
                    }

                    int thicknessBase = thicknessComp.GetVertexIndex();
                    thicknessComp.AddVertices(thicknessVerts, thicknessUVs);

                    // Start to Mid
                    thicknessComp.AddTriangle(thicknessBase, 1, 0, 4);
                    thicknessComp.AddTriangle(thicknessBase, 5, 4, 0);

                    // Mid to End
                    thicknessComp.AddTriangle(thicknessBase, 2, 1, 3);
                    thicknessComp.AddTriangle(thicknessBase, 4, 3, 1);

                    // Ends
                    thicknessComp.AddTriangle(thicknessBase, 7, 2, 3);
                    thicknessComp.AddTriangle(thicknessBase, 6, 5, 0);
                }

                if (lPattern != null)
                {
                    var lVertStart = lPattern.GetVertexIndex();
                    lPattern.AddVertices(wallVertices, wallUVs);

                    // Start to Mid
                    lPattern.AddTriangle(lVertStart, 0, 5, 4);
                    lPattern.AddTriangle(lVertStart, 4, 1, 0);

                    // Mid to End
                    lPattern.AddTriangle(lVertStart, 1, 4, 3);
                    lPattern.AddTriangle(lVertStart, 3, 2, 1);
                }

                if (rPattern != null)
                {
                    wallUVs[0].x = 1;
                    wallUVs[2].x = 0;
                    wallUVs[3].x = 0;
                    wallUVs[5].x = 1;

                    var rVertStart = rPattern.GetVertexIndex();
                    rPattern.AddVertices(isThick ? wallVertices2 : wallVertices, wallUVs);

                    // Start to Mid
                    rPattern.AddTriangle(rVertStart, 0, 1, 4);
                    rPattern.AddTriangle(rVertStart, 4, 5, 0);

                    // Mid to End
                    rPattern.AddTriangle(rVertStart, 1, 2, 3);
                    rPattern.AddTriangle(rVertStart, 3, 4, 1);
                }

                // Cap off wall ends.
                if (toI.Simple && toI.IncomingLines.Count == 1 && rPattern != null)
                {
                    float bottomV = (wallVertices[3].y - wallVertices[2].y) / WallHeight;

                    endVertices[0] = wallVertices[2];
                    endVertices[1] = wallVertices2[2];
                    endVertices[2] = wallVertices2[3];
                    endVertices[3] = wallVertices[3];

                    endUVs[0] = new Vector2(1 - Thickness * 2, bottomV);
                    endUVs[1] = new Vector2(1, bottomV);
                    endUVs[2] = new Vector2(1, 0);
                    endUVs[3] = new Vector2(1 - Thickness * 2, 0);

                    var rVertStart = rPattern.GetVertexIndex();
                    rPattern.AddVertices(endVertices, endUVs);

                    rPattern.AddTriangle(rVertStart, 0, 3, 2);
                    rPattern.AddTriangle(rVertStart, 2, 1, 0);
                }

                if (fromI.Simple && fromI.IncomingLines.Count == 1 && lPattern != null)
                {
                    float bottomV = (wallVertices[5].y - wallVertices[0].y) / WallHeight;

                    endVertices[0] = wallVertices2[0];
                    endVertices[1] = wallVertices[0];
                    endVertices[2] = wallVertices[5];
                    endVertices[3] = wallVertices2[5];

                    endUVs[0] = new Vector2(1 - Thickness * 2, bottomV);
                    endUVs[1] = new Vector2(1, bottomV);
                    endUVs[2] = new Vector2(1, 0);
                    endUVs[3] = new Vector2(1 - Thickness * 2, 0);

                    var lVertStart = lPattern.GetVertexIndex();
                    lPattern.AddVertices(endVertices, endUVs);

                    lPattern.AddTriangle(lVertStart, 0, 3, 2);
                    lPattern.AddTriangle(lVertStart, 2, 1, 0);
                }
            }

            foreach (PatternMesh pattern in _patterns)
            {
                pattern?.Component.Commit();
            }

            thicknessComp.Commit();
        }

        private void AddFencePosts()
        {
            int width = _elevationData.Width;
            int height = _elevationData.Height;
            int floors = _elevationData.Depth;
            int baseFloor = _wallGraph.BaseFloor;

            FencePost[] entries = _fencePosts.Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                ref FencePost entry = ref entries[i];

                var fence = GetFence(entry.GUID);

                fence.AddPost(entry.XPos, entry.YPos, GetElevationInt(_elevationData.Data[entry.Level - baseFloor], width, height, entry.XPos, entry.YPos));
            }
        }
    }
}