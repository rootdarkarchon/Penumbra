using System;
using Dalamud.Memory;
using Penumbra.GameData.Util;
using CharacterUtility = Penumbra.Interop.CharacterUtility;

namespace Penumbra.Meta.Files;

public unsafe class MetaBaseFile : IDisposable
{
    public byte* Data { get; private set; }
    public int Length { get; private set; }
    public CharacterUtility.InternalIndex Index { get; }

    public MetaBaseFile( Interop.Structs.CharacterUtility.Index idx )
        => Index = CharacterUtility.ReverseIndices[(int) idx];

    protected (IntPtr Data, int Length) DefaultData
        => Penumbra.CharacterUtility.DefaultResource( Index );

    // Reset to default values.
    public virtual void Reset()
    { }

    // Obtain memory.
    protected void AllocateData( int length )
    {
        Length = length;
        Data   = ( byte* )Penumbra.MetaFileManager.AllocateFileMemory( length );
        if( length > 0 )
        {
            GC.AddMemoryPressure( length );
        }
    }

    // Free memory.
    protected void ReleaseUnmanagedResources()
    {
        var ptr = ( IntPtr )Data;
        MemoryHelper.GameFree( ref ptr, ( ulong )Length );
        if( Length > 0 )
        {
            GC.RemoveMemoryPressure( Length );
        }

        Length = 0;
        Data   = null;
    }

    // Resize memory while retaining data.
    protected void ResizeResources( int newLength )
    {
        if( newLength == Length )
        {
            return;
        }

        var data = ( byte* )Penumbra.MetaFileManager.AllocateFileMemory( ( ulong )newLength );
        if( newLength > Length )
        {
            Functions.MemCpyUnchecked( data, Data, Length );
            Functions.MemSet( data + Length, 0, newLength - Length );
        }
        else
        {
            Functions.MemCpyUnchecked( data, Data, newLength );
        }

        ReleaseUnmanagedResources();
        GC.AddMemoryPressure( newLength );
        Data   = data;
        Length = newLength;
    }

    // Manually free memory. 
    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize( this );
    }

    ~MetaBaseFile()
    {
        ReleaseUnmanagedResources();
    }
}