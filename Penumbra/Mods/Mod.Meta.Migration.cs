using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Util;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    private static class Migration
    {
        public static bool Migrate( Mod mod, JObject json )
            => MigrateV0ToV1( mod, json );

        private static bool MigrateV0ToV1( Mod mod, JObject json )
        {
            if( mod.FileVersion > 0 )
            {
                return false;
            }

            var swaps = json[ "FileSwaps" ]?.ToObject< Dictionary< Utf8GamePath, FullPath > >()
             ?? new Dictionary< Utf8GamePath, FullPath >();
            var groups = json[ "Groups" ]?.ToObject< Dictionary< string, OptionGroupV0 > >() ?? new Dictionary< string, OptionGroupV0 >();
            var priority = 1;
            var seenMetaFiles = new HashSet< FullPath >();
            foreach( var group in groups.Values )
            {
                ConvertGroup( mod, group, ref priority, seenMetaFiles );
            }

            foreach( var unusedFile in mod.FindUnusedFiles().Where( f => !seenMetaFiles.Contains( f ) ) )
            {
                if( unusedFile.ToGamePath( mod.BasePath, out var gamePath )
                && !mod._default.FileData.TryAdd( gamePath, unusedFile ) )
                {
                    PluginLog.Error( $"Could not add {gamePath} because it already points to {mod._default.FileData[ gamePath ]}." );
                }
            }

            mod._default.FileSwapData.Clear();
            mod._default.FileSwapData.EnsureCapacity( swaps.Count );
            foreach( var (gamePath, swapPath) in swaps )
            {
                mod._default.FileSwapData.Add( gamePath, swapPath );
            }

            mod._default.IncorporateMetaChanges( mod.BasePath, false );
            foreach( var group in mod.Groups )
            {
                IModGroup.SaveModGroup( group, mod.BasePath );
            }

            mod.SaveDefaultMod();

            return true;
        }

        private static void ConvertGroup( Mod mod, OptionGroupV0 group, ref int priority, HashSet< FullPath > seenMetaFiles )
        {
            if( group.Options.Count == 0 )
            {
                return;
            }

            switch( group.SelectionType )
            {
                case SelectType.Multi:

                    var optionPriority = 0;
                    var newMultiGroup = new MultiModGroup()
                    {
                        Name        = group.GroupName,
                        Priority    = priority++,
                        Description = string.Empty,
                    };
                    mod._groups.Add( newMultiGroup );
                    foreach( var option in group.Options )
                    {
                        newMultiGroup.PrioritizedOptions.Add( ( SubModFromOption( mod.BasePath, option, seenMetaFiles ), optionPriority++ ) );
                    }

                    break;
                case SelectType.Single:
                    if( group.Options.Count == 1 )
                    {
                        AddFilesToSubMod( mod._default, mod.BasePath, group.Options[ 0 ], seenMetaFiles );
                        return;
                    }

                    var newSingleGroup = new SingleModGroup()
                    {
                        Name        = group.GroupName,
                        Priority    = priority++,
                        Description = string.Empty,
                    };
                    mod._groups.Add( newSingleGroup );
                    foreach( var option in group.Options )
                    {
                        newSingleGroup.OptionData.Add( SubModFromOption( mod.BasePath, option, seenMetaFiles ) );
                    }

                    break;
            }
        }

        private static void AddFilesToSubMod( SubMod mod, DirectoryInfo basePath, OptionV0 option, HashSet< FullPath > seenMetaFiles )
        {
            foreach( var (relPath, gamePaths) in option.OptionFiles )
            {
                var fullPath = new FullPath( basePath, relPath );
                foreach( var gamePath in gamePaths )
                {
                    mod.FileData.TryAdd( gamePath, fullPath );
                }

                if( fullPath.Extension is ".meta" or ".rgsp" )
                {
                    seenMetaFiles.Add( fullPath );
                }
            }
        }

        private static SubMod SubModFromOption( DirectoryInfo basePath, OptionV0 option, HashSet< FullPath > seenMetaFiles )
        {
            var subMod = new SubMod { Name = option.OptionName };
            AddFilesToSubMod( subMod, basePath, option, seenMetaFiles );
            subMod.IncorporateMetaChanges( basePath, false );
            return subMod;
        }

        private struct OptionV0
        {
            public string OptionName = string.Empty;
            public string OptionDesc = string.Empty;

            [JsonProperty( ItemConverterType = typeof( SingleOrArrayConverter< Utf8GamePath > ) )]
            public Dictionary< Utf8RelPath, HashSet< Utf8GamePath > > OptionFiles = new();

            public OptionV0()
            { }
        }

        private struct OptionGroupV0
        {
            public string GroupName = string.Empty;

            [JsonConverter( typeof( Newtonsoft.Json.Converters.StringEnumConverter ) )]
            public SelectType SelectionType = SelectType.Single;

            public List< OptionV0 > Options = new();

            public OptionGroupV0()
            { }
        }

        // Not used anymore, but required for migration.
        private class SingleOrArrayConverter< T > : JsonConverter
        {
            public override bool CanConvert( Type objectType )
                => objectType == typeof( HashSet< T > );

            public override object ReadJson( JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer )
            {
                var token = JToken.Load( reader );

                if( token.Type == JTokenType.Array )
                {
                    return token.ToObject< HashSet< T > >() ?? new HashSet< T >();
                }

                var tmp = token.ToObject< T >();
                return tmp != null
                    ? new HashSet< T > { tmp }
                    : new HashSet< T >();
            }

            public override bool CanWrite
                => true;

            public override void WriteJson( JsonWriter writer, object? value, JsonSerializer serializer )
            {
                writer.WriteStartArray();
                if( value != null )
                {
                    var v = ( HashSet< T > )value;
                    foreach( var val in v )
                    {
                        serializer.Serialize( writer, val?.ToString() );
                    }
                }

                writer.WriteEndArray();
            }
        }
    }
}