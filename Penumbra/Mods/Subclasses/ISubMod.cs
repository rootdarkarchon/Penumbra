using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Mods;

public interface ISubMod
{
    public string Name { get; }
    public string FullName { get; }

    public IReadOnlyDictionary< Utf8GamePath, FullPath > Files { get; }
    public IReadOnlyDictionary< Utf8GamePath, FullPath > FileSwaps { get; }
    public IReadOnlySet< MetaManipulation > Manipulations { get; }

    public bool IsDefault { get; }

    public static void WriteSubMod( JsonWriter j, JsonSerializer serializer, ISubMod mod, DirectoryInfo basePath, int? priority )
    {
        j.WriteStartObject();
        j.WritePropertyName( nameof( Name ) );
        j.WriteValue( mod.Name );
        if( priority != null )
        {
            j.WritePropertyName( nameof( IModGroup.Priority ) );
            j.WriteValue( priority.Value );
        }

        j.WritePropertyName( nameof( mod.Files ) );
        j.WriteStartObject();
        foreach( var (gamePath, file) in mod.Files )
        {
            if( file.ToRelPath( basePath, out var relPath ) )
            {
                j.WritePropertyName( gamePath.ToString() );
                j.WriteValue( relPath.ToString() );
            }
        }

        j.WriteEndObject();
        j.WritePropertyName( nameof( mod.FileSwaps ) );
        j.WriteStartObject();
        foreach( var (gamePath, file) in mod.FileSwaps )
        {
            j.WritePropertyName( gamePath.ToString() );
            j.WriteValue( file.ToString() );
        }

        j.WriteEndObject();
        j.WritePropertyName( nameof( mod.Manipulations ) );
        serializer.Serialize( j, mod.Manipulations );
        j.WriteEndObject();
    }
}