namespace Vfx
{
    public readonly struct VfxEmissionToken
    {
        public readonly uint Id;

        internal VfxEmissionToken(uint id) => Id = id;

        public bool IsValid => Id != 0;

        public override string ToString() => $"Emission({Id})";
    }
}
