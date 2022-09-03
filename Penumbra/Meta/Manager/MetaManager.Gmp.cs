using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OtterGui.Filesystem;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Meta.Manager;

public partial class MetaManager
{
    private          ExpandedGmpFile?        _gmpFile          = null;
    private readonly List< GmpManipulation > _gmpManipulations = new();

    public void SetGmpFiles()
        => SetFile( _gmpFile, CharacterUtility.Index.Gmp );

    public static void ResetGmpFiles()
        => SetFile( null, CharacterUtility.Index.Gmp );

    public void ResetGmp()
    {
        if( _gmpFile == null )
        {
            return;
        }

        _gmpFile.Reset( _gmpManipulations.Select( m => ( int )m.SetId ) );
        _gmpManipulations.Clear();
    }

    public bool ApplyMod( GmpManipulation manip )
    {
        _gmpManipulations.AddOrReplace( manip );
        _gmpFile ??= new ExpandedGmpFile();
        return manip.Apply( _gmpFile );
    }

    public bool RevertMod( GmpManipulation manip )
    {
        if( _gmpManipulations.Remove( manip ) )
        {
            var def = ExpandedGmpFile.GetDefault( manip.SetId );
            manip = new GmpManipulation( def, manip.SetId );
            return manip.Apply( _gmpFile! );
        }

        return false;
    }

    public void DisposeGmp()
    {
        _gmpFile?.Dispose();
        _gmpFile = null;
        _gmpManipulations.Clear();
    }
}