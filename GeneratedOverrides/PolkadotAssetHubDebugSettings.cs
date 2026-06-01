using System;
using Substrate.NetApi.Attributes;
using Substrate.NetApi.Model.Types.Base;
using Substrate.NetApi.Model.Types.Metadata.Base;

namespace PolkadotAssetHub.NetApi.Generated.Model.pallet_revive.debug;

[SubstrateNodeType(TypeDefEnum.Composite)]
public sealed class DebugSettings : BaseType
{
    public override string TypeName()
    {
        return "DebugSettings";
    }

    public override byte[] Encode()
    {
        return Array.Empty<byte>();
    }

    public override void Decode(byte[] byteArray, ref int p)
    {
        TypeSize = 0;
        Bytes = Array.Empty<byte>();
    }
}
