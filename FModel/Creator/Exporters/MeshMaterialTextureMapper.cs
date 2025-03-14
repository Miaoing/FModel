using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Linq;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using FModel.Creator.Exporters.Models;
using CUE4Parse.UE4.Assets;
using Serilog;
using System.Text;

namespace FModel.Creator.Exporters
{
    public class MeshMaterialTextureMapper
    {
        private readonly List<MappingEntry> _entries = new();
        private readonly Regex _diffuseTexturePattern = new(@"(diffuse|albedo|basecolor)", RegexOptions.IgnoreCase);
        private readonly Regex _normalTexturePattern = new(@"(normal|norm)", RegexOptions.IgnoreCase);

        private string GetObjectName(string fullPath)
        {
            var parts = fullPath.Split('/');
            if (parts.Length == 0) return fullPath;

            var fileName = parts[parts.Length - 1].Split('.')[0];
            
            if (parts.Length == 1) return fileName;
            
            var directoryPath = string.Join("/", parts.Take(parts.Length - 1));
            return directoryPath + "/" + fileName;
        }

        public void ProcessPackage(IPackage package)
        {
            for (var i = 0; i < package.ExportMapLength; i++)
            {
                try
                {
                    var export = package.ExportsLazy[i].Value;
                    if (export is UStaticMesh staticMesh)
                    {
                        ProcessStaticMesh(staticMesh);
                    }
                    else if (export is USkeletalMesh skeletalMesh)
                    {
                        ProcessSkeletalMesh(skeletalMesh);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Unexpected error processing export at index {i} in package {package.Name}: {ex.Message}");
                    continue;
                }
            }
        }

        private void ProcessStaticMesh(UStaticMesh mesh)
        {
            try
            {
                foreach (var material in mesh.Materials)
                {
                    if (material?.Load<UMaterialInterface>() is { } loadedMaterial)
                    {
                        ProcessMaterial(GetObjectName(mesh.GetPathName()), loadedMaterial);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to process static mesh {mesh.GetPathName()}: {e.Message}");
            }
        }

        private void ProcessSkeletalMesh(USkeletalMesh mesh)
        {
            try
            {
                foreach (var material in mesh.Materials)
                {
                    if (material?.Load<UMaterialInterface>() is { } loadedMaterial)
                    {
                        ProcessMaterial(GetObjectName(mesh.GetPathName()), loadedMaterial);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to process skeletal mesh {mesh.GetPathName()}: {e.Message}");
            }
        }

        private void ProcessMaterial(string meshPath, UMaterialInterface material)
        {
            try
            {
                var entry = new MappingEntry
                {
                    MeshPath = meshPath,
                    MaterialPath = GetObjectName(material.GetPathName())
                };

                var parameters = new CMaterialParams2();
                material.GetParams(parameters, EMaterialFormat.FirstLayer);

                // 收集所有贴图路径
                foreach (var texture in parameters.Textures)
                {
                    entry.AllTexturePaths.Add(GetObjectName(texture.Value.GetPathName()));
                }

                // 对于 Diffuse，按照优先级尝试三种来源
                if (parameters.HasTopDiffuse)
                {
                    // 1. 尝试获取明确的 Diffuse 贴图
                    foreach (var name in CMaterialParams2.Diffuse[0])
                    {
                        if (parameters.Textures.TryGetValue(name, out var texture))
                        {
                            entry.DiffuseTexturePath = GetObjectName(texture.GetPathName());
                            break;
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(entry.DiffuseTexturePath))
                {
                    // 2. 尝试获取回退 Diffuse 贴图
                    if (parameters.Textures.TryGetValue(CMaterialParams2.FallbackDiffuse, out var fallbackTexture))
                    {
                        entry.DiffuseTexturePath = GetObjectName(fallbackTexture.GetPathName());
                    }
                    // 3. 如果还是没有，使用第一个可用的贴图，但要确保它不是 normal 贴图
                    else if (parameters.TryGetFirstTexture2d(out var firstTexture))
                    {
                        // 使用 VerifyTexture 方法检查贴图是否是 normal 贴图
                        // 创建一个临时的 CMaterialParams2 对象来测试贴图类型
                        var tempParams = new CMaterialParams2();
                        bool isNormalTexture = tempParams.VerifyTexture(firstTexture.Name, firstTexture, false, EMaterialSamplerType.SAMPLERTYPE_Normal);
                        
                        // 如果 VerifyTexture 返回 true，说明它是 normal 贴图
                        // 如果返回 false，说明它不是 normal 贴图
                        if (!isNormalTexture)
                        {
                            entry.DiffuseTexturePath = GetObjectName(firstTexture.GetPathName());
                        }
                    }
                }

                // 对于 Normal，只尝试两种来源
                if (parameters.HasTopNormals)
                {
                    // 1. 尝试获取明确的 Normal 贴图
                    foreach (var name in CMaterialParams2.Normals[0])
                    {
                        if (parameters.Textures.TryGetValue(name, out var texture))
                        {
                            entry.NormalTexturePath = GetObjectName(texture.GetPathName());
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(entry.NormalTexturePath))
                {
                    // 2. 尝试获取回退 Normal 贴图
                    if (parameters.Textures.TryGetValue(CMaterialParams2.FallbackNormals, out var fallbackTexture))
                    {
                        entry.NormalTexturePath = GetObjectName(fallbackTexture.GetPathName());
                    }
                }

                _entries.Add(entry);
            }
            catch (Exception e)
            {
                Log.Warning($"Failed to process material {material.GetPathName()}: {e.Message}");
            }
        }

        public void ExportToCsv(string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 找出所有条目中最大的贴图数量
            int maxTextureCount = 0;
            foreach (var entry in _entries)
            {
                maxTextureCount = Math.Max(maxTextureCount, entry.AllTexturePaths.Count);
            }

            using var writer = new StreamWriter(outputPath);
            
            // 写入标题行
            var headerBuilder = new StringBuilder("MeshPath,MaterialPath,DiffuseTexturePath,NormalTexturePath");
            for (int i = 1; i <= maxTextureCount; i++)
            {
                headerBuilder.Append($",Tex{i}");
            }
            writer.WriteLine(headerBuilder.ToString());

            // 写入数据行
            foreach (var entry in _entries)
            {
                var lineBuilder = new StringBuilder($"{entry.MeshPath},{entry.MaterialPath},{entry.DiffuseTexturePath},{entry.NormalTexturePath}");
                
                // 添加所有贴图路径
                for (int i = 0; i < maxTextureCount; i++)
                {
                    if (i < entry.AllTexturePaths.Count)
                    {
                        lineBuilder.Append($",{entry.AllTexturePaths[i]}");
                    }
                    else
                    {
                        lineBuilder.Append(",");
                    }
                }
                
                writer.WriteLine(lineBuilder.ToString());
            }
        }
    }
} 
