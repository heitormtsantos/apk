using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BattleRoyale.Editor
{
    public static class WardrobeSampleAssetBuilder
    {
        private const string SourcePath = "Assets/_Game/Resources/UserCharacters/Steve/Model/Steve.fbx";
        private const string Root = "Assets/_Game/Resources/Wardrobe";
        private const string Prefabs = Root + "/Prefabs";
        private const string Meshes = Root + "/Meshes";
        private const string Materials = Root + "/Materials";
        private const string Items = Root + "/Items";
        private const string Icons = Root + "/Icons";

        private readonly struct Piece
        {
            public Piece(HumanBodyBones bone, Vector3 center, Vector3 size)
            {
                Bone = bone;
                Center = center;
                Size = size;
            }

            public HumanBodyBones Bone { get; }
            public Vector3 Center { get; }
            public Vector3 Size { get; }
        }

        [MenuItem("Raven Drop/Wardrobe/Rebuild Starter Wardrobe")]
        public static void Build()
        {
            EnsureFolders();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new InvalidOperationException($"Missing character source at {SourcePath}.");

            var cyan = CreateMaterial("Cloth_Cyan", new Color(0.03f, 0.42f, 0.50f), 0.15f, 0.72f);
            var red = CreateMaterial("Cloth_Red", new Color(0.55f, 0.055f, 0.045f), 0.12f, 0.66f);
            var black = CreateMaterial("Cloth_Black", new Color(0.018f, 0.025f, 0.03f), 0.22f, 0.52f);
            var olive = CreateMaterial("Cloth_Olive", new Color(0.16f, 0.23f, 0.08f), 0.10f, 0.58f);
            var denim = CreateMaterial("Cloth_Denim", new Color(0.035f, 0.11f, 0.24f), 0.08f, 0.62f);
            var white = CreateMaterial("Cloth_White", new Color(0.72f, 0.76f, 0.76f), 0.16f, 0.74f);
            var amber = CreateMaterial("Cloth_Amber", new Color(0.84f, 0.34f, 0.025f), 0.18f, 0.68f);

            var shirtPrefab = CreateGarmentPrefab(source, "Shirt_Base", ClothingSlot.Shirt, cyan);
            var pantsPrefab = CreateGarmentPrefab(source, "Pants_Base", ClothingSlot.Pants, denim);
            var shoesPrefab = CreateGarmentPrefab(source, "Shoes_Base", ClothingSlot.Shoes, black);
            var hairPrefab = CreateGarmentPrefab(source, "Hair_Base", ClothingSlot.Hair, black);
            var hatPrefab = CreateGarmentPrefab(source, "Hat_Base", ClothingSlot.Hat, olive);
            var facePrefab = CreateGarmentPrefab(source, "Face_Base", ClothingSlot.Face, black);

            var all = new List<ClothingItemData>
            {
                CreateItem("shirt_default", "Camiseta Raven", ClothingSlot.Shirt, shirtPrefab, cyan,
                    new[] { BodyPart.Torso }, true),
                CreateItem("shirt_red", "Camiseta Vermelha", ClothingSlot.Shirt, shirtPrefab, red,
                    new[] { BodyPart.Torso }),
                CreateItem("shirt_military", "Jaqueta Militar", ClothingSlot.Shirt, shirtPrefab, olive,
                    new[] { BodyPart.Torso, BodyPart.Arms }, false, ClothingRarity.Rare),
                CreateItem("pants_default", "Calca Tatica", ClothingSlot.Pants, pantsPrefab, denim,
                    new[] { BodyPart.Legs }, true),
                CreateItem("pants_black", "Calca Preta", ClothingSlot.Pants, pantsPrefab, black,
                    new[] { BodyPart.Legs }),
                CreateItem("pants_cargo", "Calca Cargo", ClothingSlot.Pants, pantsPrefab, olive,
                    new[] { BodyPart.Legs }, false, ClothingRarity.Rare),
                CreateItem("shoes_default", "Tenis Raven", ClothingSlot.Shoes, shoesPrefab, white,
                    new[] { BodyPart.Feet }, true),
                CreateItem("shoes_black", "Tenis Preto", ClothingSlot.Shoes, shoesPrefab, black,
                    new[] { BodyPart.Feet }),
                CreateItem("shoes_amber", "Botas Amber", ClothingSlot.Shoes, shoesPrefab, amber,
                    new[] { BodyPart.Feet }, false, ClothingRarity.Uncommon),
                CreateItem("hair_default", "Cabelo Curto", ClothingSlot.Hair, hairPrefab, black,
                    Array.Empty<BodyPart>(), true),
                CreateItem("hair_amber", "Cabelo Amber", ClothingSlot.Hair, hairPrefab, amber,
                    Array.Empty<BodyPart>()),
                CreateItem("hat_military", "Boina Militar", ClothingSlot.Hat, hatPrefab, olive,
                    Array.Empty<BodyPart>(), false, ClothingRarity.Rare),
                CreateItem("hat_raven", "Bone Raven", ClothingSlot.Hat, hatPrefab, black,
                    Array.Empty<BodyPart>()),
                CreateItem("face_mask", "Mascara Tatica", ClothingSlot.Face, facePrefab, black,
                    Array.Empty<BodyPart>()),
                CreateItem("face_amber", "Mascara Amber", ClothingSlot.Face, facePrefab, amber,
                    Array.Empty<BodyPart>(), false, ClothingRarity.Epic)
            };

            var database = CreateOrReplace<ClothingDatabase>(Root + "/ClothingDatabase.asset");
            database.ConfigureEditor(all);
            EditorUtility.SetDirty(database);

            var bundle = CreateOrReplace<OutfitBundleData>(Root + "/RavenMilitaryBundle.asset");
            bundle.ConfigureEditor("bundle_raven_military", "Raven Military", new[]
            {
                Find(all, "hair_default"), Find(all, "hat_military"), Find(all, "face_mask"),
                Find(all, "shirt_military"), Find(all, "pants_cargo"), Find(all, "shoes_amber")
            });
            EditorUtility.SetDirty(bundle);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"WARDROBE_BUILD_COMPLETE items={all.Count} database={AssetDatabase.GetAssetPath(database)}");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static GameObject CreateGarmentPrefab(GameObject source, string assetName, ClothingSlot slot,
            Material material)
        {
            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not instantiate wardrobe source character.");
            try
            {
                instance.name = assetName;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                    throw new InvalidOperationException("Wardrobe source must contain a humanoid Animator.");
                animator.enabled = false;

                var sourceRenderer = WardrobeSourceRenderer(instance, slot);
                if (sourceRenderer == null) throw new InvalidOperationException("Wardrobe source has no skinned mesh.");
                var rendererRoot = sourceRenderer.transform;
                var rendererBounds = sourceRenderer.localBounds;
                Mesh mesh;
                Transform[] bones;
                Transform rootBone;
                try
                {
                    mesh = BuildExtractedMesh(assetName + " Mesh", sourceRenderer, animator, slot,
                        out bones, out rootBone);
                }
                catch (InvalidOperationException)
                {
                    sourceRenderer = LargestRenderer(instance);
                    rendererRoot = sourceRenderer.transform;
                    rendererBounds = sourceRenderer.localBounds;
                    mesh = BuildExtractedMesh(assetName + " Mesh", sourceRenderer, animator, slot,
                        out bones, out rootBone);
                }
                foreach (var existingRenderer in instance.GetComponentsInChildren<Renderer>(true))
                    UnityEngine.Object.DestroyImmediate(existingRenderer);
                var renderer = rendererRoot.gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.name = assetName + " Renderer";
                var meshPath = Meshes + "/" + assetName + ".asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);
                renderer.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.bones = bones;
                renderer.rootBone = rootBone;
                renderer.localBounds = rendererBounds;
                renderer.updateWhenOffscreen = false;

                var prefabPath = Prefabs + "/" + assetName + ".prefab";
                AssetDatabase.DeleteAsset(prefabPath);
                return PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Mesh BuildExtractedMesh(string meshName, SkinnedMeshRenderer source, Animator animator,
            ClothingSlot slot, out Transform[] bones, out Transform rootBone)
        {
            var sourceMesh = source.sharedMesh;
            var sourceVertices = sourceMesh.vertices;
            var sourceNormals = sourceMesh.normals;
            var sourceUv = sourceMesh.uv;
            var sourceWeights = sourceMesh.boneWeights;
            var remap = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var triangles = new List<int>();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var headLocal = head != null ? source.transform.InverseTransformPoint(head.position) : Vector3.zero;
            var forwardLocal = source.transform.InverseTransformDirection(animator.transform.forward).normalized;
            var expansion = Expansion(slot);

            for (var subMesh = 0; subMesh < sourceMesh.subMeshCount; subMesh++)
            {
                var sourceTriangles = sourceMesh.GetTriangles(subMesh);
                for (var triangle = 0; triangle + 2 < sourceTriangles.Length; triangle += 3)
                {
                    var a = sourceTriangles[triangle];
                    var b = sourceTriangles[triangle + 1];
                    var c = sourceTriangles[triangle + 2];
                    var center = (sourceVertices[a] + sourceVertices[b] + sourceVertices[c]) / 3f;
                    var matches = MatchesSlot(source, sourceWeights[a], slot, center, headLocal, forwardLocal)
                        + MatchesSlot(source, sourceWeights[b], slot, center, headLocal, forwardLocal)
                        + MatchesSlot(source, sourceWeights[c], slot, center, headLocal, forwardLocal);
                    if (matches < 2) continue;
                    triangles.Add(CopyVertex(a));
                    triangles.Add(CopyVertex(b));
                    triangles.Add(CopyVertex(c));
                }
            }

            if (triangles.Count == 0) throw new InvalidOperationException($"No source triangles matched clothing slot {slot}.");
            bones = source.bones;
            rootBone = source.rootBone;
            var mesh = new Mesh { name = meshName, indexFormat = sourceMesh.indexFormat };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = sourceMesh.bindposes;
            mesh.RecalculateBounds();
            return mesh;

            int CopyVertex(int sourceIndex)
            {
                if (remap.TryGetValue(sourceIndex, out var existing)) return existing;
                var index = vertices.Count;
                remap.Add(sourceIndex, index);
                var normal = sourceNormals.Length == sourceVertices.Length ? sourceNormals[sourceIndex] : Vector3.up;
                vertices.Add(sourceVertices[sourceIndex] + normal.normalized * expansion);
                normals.Add(normal);
                uv.Add(sourceUv.Length == sourceVertices.Length ? sourceUv[sourceIndex] : Vector2.zero);
                weights.Add(sourceWeights[sourceIndex]);
                return index;
            }
        }

        private static int MatchesSlot(SkinnedMeshRenderer source, BoneWeight weight, ClothingSlot slot,
            Vector3 center, Vector3 headLocal, Vector3 forwardLocal)
        {
            var boneIndex = DominantBone(weight);
            if (boneIndex < 0 || boneIndex >= source.bones.Length || source.bones[boneIndex] == null) return 0;
            var bone = CharacterBoneMapper.CanonicalName(source.bones[boneIndex].name);
            if (slot == ClothingSlot.Shirt)
                return bone is "Spine" or "Spine1" or "Spine2" or "LeftShoulder" or "RightShoulder"
                    or "LeftArm" or "RightArm" ? 1 : 0;
            if (slot == ClothingSlot.Pants)
                return bone is "Hips" or "LeftUpLeg" or "RightUpLeg" or "LeftLeg" or "RightLeg" ? 1 : 0;
            if (slot == ClothingSlot.Shoes)
                return bone is "LeftFoot" or "RightFoot" or "LeftToeBase" or "RightToeBase" ? 1 : 0;
            if (bone != "Head") return 0;
            if (slot == ClothingSlot.Hair) return center.y >= headLocal.y ? 1 : 0;
            if (slot == ClothingSlot.Hat) return center.y >= headLocal.y + 0.025f ? 1 : 0;
            if (slot == ClothingSlot.Face)
                return center.y <= headLocal.y + 0.035f
                    && Vector3.Dot(center - headLocal, forwardLocal) >= 0f ? 1 : 0;
            return 0;
        }

        private static int DominantBone(BoneWeight weight)
        {
            var index = weight.boneIndex0;
            var value = weight.weight0;
            if (weight.weight1 > value) { index = weight.boneIndex1; value = weight.weight1; }
            if (weight.weight2 > value) { index = weight.boneIndex2; value = weight.weight2; }
            if (weight.weight3 > value) index = weight.boneIndex3;
            return index;
        }

        private static float Expansion(ClothingSlot slot) => slot switch
        {
            ClothingSlot.Shirt => 0.012f,
            ClothingSlot.Pants => 0.010f,
            ClothingSlot.Shoes => 0.014f,
            ClothingSlot.Hair => 0.018f,
            ClothingSlot.Hat => 0.050f,
            ClothingSlot.Face => 0.022f,
            _ => 0.01f
        };

        private static SkinnedMeshRenderer WardrobeSourceRenderer(GameObject instance, ClothingSlot slot)
        {
            SkinnedMeshRenderer largest = null;
            var largestVertices = -1;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var facialSlot = slot is ClothingSlot.Hair or ClothingSlot.Hat or ClothingSlot.Face;
                var hasEyelashes = false;
                var materials = renderers[i].sharedMaterials;
                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    hasEyelashes |= materials[materialIndex] != null
                        && materials[materialIndex].name.Contains("eyelashes", StringComparison.OrdinalIgnoreCase);
                if (facialSlot != hasEyelashes) continue;
                var count = renderers[i].sharedMesh != null ? renderers[i].sharedMesh.vertexCount : 0;
                if (count <= largestVertices) continue;
                largest = renderers[i];
                largestVertices = count;
            }
            if (largest != null) return largest;
            for (var i = 0; i < renderers.Length; i++)
            {
                var count = renderers[i].sharedMesh != null ? renderers[i].sharedMesh.vertexCount : 0;
                if (count <= largestVertices) continue;
                largest = renderers[i];
                largestVertices = count;
            }
            return largest;
        }

        private static SkinnedMeshRenderer LargestRenderer(GameObject instance)
        {
            SkinnedMeshRenderer largest = null;
            var largestVertices = -1;
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var count = renderers[i].sharedMesh != null ? renderers[i].sharedMesh.vertexCount : 0;
                if (count <= largestVertices) continue;
                largest = renderers[i];
                largestVertices = count;
            }
            return largest;
        }

        private static Piece[] BuildPieces(Animator animator, Transform root, ClothingSlot slot)
        {
            Vector3 Point(HumanBodyBones bone) => root.InverseTransformPoint(RequireBone(animator, bone).position);
            var head = Point(HumanBodyBones.Head);
            var chest = Point(HumanBodyBones.Chest);
            var hips = Point(HumanBodyBones.Hips);
            var leftUpperArm = Point(HumanBodyBones.LeftUpperArm);
            var leftLowerArm = Point(HumanBodyBones.LeftLowerArm);
            var rightUpperArm = Point(HumanBodyBones.RightUpperArm);
            var rightLowerArm = Point(HumanBodyBones.RightLowerArm);
            var leftUpperLeg = Point(HumanBodyBones.LeftUpperLeg);
            var leftLowerLeg = Point(HumanBodyBones.LeftLowerLeg);
            var rightUpperLeg = Point(HumanBodyBones.RightUpperLeg);
            var rightLowerLeg = Point(HumanBodyBones.RightLowerLeg);
            var leftFoot = Point(HumanBodyBones.LeftFoot);
            var rightFoot = Point(HumanBodyBones.RightFoot);

            return slot switch
            {
                ClothingSlot.Shirt => new[]
                {
                    new Piece(HumanBodyBones.Chest, Vector3.Lerp(hips, chest, 0.68f), new Vector3(0.54f, 0.46f, 0.29f)),
                    new Piece(HumanBodyBones.LeftUpperArm, Vector3.Lerp(leftUpperArm, leftLowerArm, 0.35f), new Vector3(0.29f, 0.18f, 0.20f)),
                    new Piece(HumanBodyBones.RightUpperArm, Vector3.Lerp(rightUpperArm, rightLowerArm, 0.35f), new Vector3(0.29f, 0.18f, 0.20f))
                },
                ClothingSlot.Pants => new[]
                {
                    new Piece(HumanBodyBones.Hips, hips + Vector3.down * 0.09f, new Vector3(0.43f, 0.24f, 0.27f)),
                    new Piece(HumanBodyBones.LeftUpperLeg, Vector3.Lerp(leftUpperLeg, leftLowerLeg, 0.48f), new Vector3(0.19f, 0.48f, 0.23f)),
                    new Piece(HumanBodyBones.RightUpperLeg, Vector3.Lerp(rightUpperLeg, rightLowerLeg, 0.48f), new Vector3(0.19f, 0.48f, 0.23f)),
                    new Piece(HumanBodyBones.LeftLowerLeg, Vector3.Lerp(leftLowerLeg, leftFoot, 0.42f), new Vector3(0.16f, 0.42f, 0.20f)),
                    new Piece(HumanBodyBones.RightLowerLeg, Vector3.Lerp(rightLowerLeg, rightFoot, 0.42f), new Vector3(0.16f, 0.42f, 0.20f))
                },
                ClothingSlot.Shoes => new[]
                {
                    new Piece(HumanBodyBones.LeftFoot, leftFoot + new Vector3(0f, 0.015f, 0.07f), new Vector3(0.19f, 0.14f, 0.34f)),
                    new Piece(HumanBodyBones.RightFoot, rightFoot + new Vector3(0f, 0.015f, 0.07f), new Vector3(0.19f, 0.14f, 0.34f))
                },
                ClothingSlot.Hair => new[]
                {
                    new Piece(HumanBodyBones.Head, head + Vector3.up * 0.105f, new Vector3(0.33f, 0.18f, 0.31f))
                },
                ClothingSlot.Hat => new[]
                {
                    new Piece(HumanBodyBones.Head, head + Vector3.up * 0.17f, new Vector3(0.40f, 0.08f, 0.42f)),
                    new Piece(HumanBodyBones.Head, head + Vector3.up * 0.24f, new Vector3(0.29f, 0.13f, 0.29f))
                },
                ClothingSlot.Face => new[]
                {
                    new Piece(HumanBodyBones.Head, head + new Vector3(0f, -0.04f, 0.145f), new Vector3(0.29f, 0.17f, 0.055f))
                },
                _ => Array.Empty<Piece>()
            };
        }

        private static Mesh BuildMesh(string meshName, Transform root, Animator animator, Piece[] pieces,
            out Transform[] bones)
        {
            var boneList = new List<Transform>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var weights = new List<BoneWeight>();
            for (var i = 0; i < pieces.Length; i++)
            {
                var bone = RequireBone(animator, pieces[i].Bone);
                var boneIndex = boneList.IndexOf(bone);
                if (boneIndex < 0)
                {
                    boneIndex = boneList.Count;
                    boneList.Add(bone);
                }
                AddBox(pieces[i].Center, pieces[i].Size, boneIndex, vertices, normals, uv, triangles, weights);
            }

            bones = boneList.ToArray();
            var bindPoses = new Matrix4x4[bones.Length];
            for (var i = 0; i < bones.Length; i++) bindPoses[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bindPoses;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBox(Vector3 center, Vector3 size, int boneIndex, List<Vector3> vertices,
            List<Vector3> normals, List<Vector2> uv, List<int> triangles, List<BoneWeight> weights)
        {
            var h = size * 0.5f;
            var corners = new[]
            {
                center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
                center + new Vector3(h.x, h.y, -h.z), center + new Vector3(-h.x, h.y, -h.z),
                center + new Vector3(-h.x, -h.y, h.z), center + new Vector3(h.x, -h.y, h.z),
                center + new Vector3(h.x, h.y, h.z), center + new Vector3(-h.x, h.y, h.z)
            };
            var faces = new[,]
            {
                { 0, 3, 2, 1 }, { 4, 5, 6, 7 }, { 0, 4, 7, 3 },
                { 1, 2, 6, 5 }, { 3, 7, 6, 2 }, { 0, 1, 5, 4 }
            };
            var faceNormals = new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            var weight = new BoneWeight { boneIndex0 = boneIndex, weight0 = 1f };
            for (var face = 0; face < 6; face++)
            {
                var start = vertices.Count;
                for (var corner = 0; corner < 4; corner++)
                {
                    vertices.Add(corners[faces[face, corner]]);
                    normals.Add(faceNormals[face]);
                    uv.Add(corner switch { 0 => Vector2.zero, 1 => Vector2.right, 2 => Vector2.one, _ => Vector2.up });
                    weights.Add(weight);
                }
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
        }

        private static ClothingItemData CreateItem(string id, string label, ClothingSlot slot, GameObject prefab,
            Material material, BodyPart[] hidden, bool isDefault = false,
            ClothingRarity rarity = ClothingRarity.Common)
        {
            var item = CreateOrReplace<ClothingItemData>(Items + "/" + id + ".asset");
            var icon = CreateIcon(id, slot, material.GetColor("_BaseColor"));
            item.ConfigureEditor(id, label, slot, prefab, new[] { material }, hidden, true, isDefault, rarity,
                0, icon);
            EditorUtility.SetDirty(item);
            return item;
        }

        private static Sprite CreateIcon(string id, ClothingSlot slot, Color color)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = id + " Icon" };
            var pixels = new Color32[size * size];
            var fill = (Color32)color;
            var shadow = (Color32)new Color(0.015f, 0.025f, 0.03f, 0.95f);
            DrawShape(pixels, size, slot, shadow, 3);
            DrawShape(pixels, size, slot, fill, 0);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var path = Icons + "/" + id + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.maxTextureSize = size;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void DrawShape(Color32[] pixels, int size, ClothingSlot slot, Color32 color, int expansion)
        {
            void Rect(int x0, int y0, int x1, int y1)
            {
                x0 = Mathf.Clamp(x0 - expansion, 0, size - 1);
                y0 = Mathf.Clamp(y0 - expansion, 0, size - 1);
                x1 = Mathf.Clamp(x1 + expansion, 0, size - 1);
                y1 = Mathf.Clamp(y1 + expansion, 0, size - 1);
                for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++) pixels[y * size + x] = color;
            }

            void Ellipse(int cx, int cy, int rx, int ry)
            {
                rx += expansion;
                ry += expansion;
                for (var y = Mathf.Max(0, cy - ry); y <= Mathf.Min(size - 1, cy + ry); y++)
                for (var x = Mathf.Max(0, cx - rx); x <= Mathf.Min(size - 1, cx + rx); x++)
                {
                    var nx = (x - cx) / (float)Mathf.Max(1, rx);
                    var ny = (y - cy) / (float)Mathf.Max(1, ry);
                    if (nx * nx + ny * ny <= 1f) pixels[y * size + x] = color;
                }
            }

            switch (slot)
            {
                case ClothingSlot.Shirt:
                    Rect(39, 34, 89, 102); Rect(20, 40, 42, 70); Rect(86, 40, 108, 70);
                    break;
                case ClothingSlot.Pants:
                    Rect(36, 31, 92, 62); Rect(37, 58, 61, 108); Rect(67, 58, 91, 108);
                    break;
                case ClothingSlot.Shoes:
                    Rect(19, 68, 59, 91); Rect(69, 68, 109, 91); Rect(12, 84, 59, 101); Rect(69, 84, 116, 101);
                    break;
                case ClothingSlot.Hair:
                    Ellipse(64, 67, 35, 38); Rect(27, 64, 101, 91);
                    break;
                case ClothingSlot.Hat:
                    Ellipse(64, 65, 31, 28); Rect(17, 76, 111, 88);
                    break;
                case ClothingSlot.Face:
                    Ellipse(64, 65, 37, 40); Rect(30, 64, 98, 91);
                    break;
            }
        }

        private static Material CreateMaterial(string assetName, Color color, float metallic, float smoothness)
        {
            var path = Materials + "/" + assetName + ".mat";
            AssetDatabase.DeleteAsset(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = new Material(shader) { name = assetName };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T CreateOrReplace<T>(string path) where T : ScriptableObject
        {
            AssetDatabase.DeleteAsset(path);
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static ClothingItemData Find(List<ClothingItemData> items, string id)
        {
            for (var i = 0; i < items.Count; i++) if (items[i].Id == id) return items[i];
            return null;
        }

        private static Transform RequireBone(Animator animator, HumanBodyBones bone)
        {
            var result = animator.GetBoneTransform(bone);
            if (result == null) throw new InvalidOperationException($"Source character has no humanoid bone {bone}.");
            return result;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game/Resources", "Wardrobe");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Meshes");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Items");
            EnsureFolder(Root, "Icons");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
