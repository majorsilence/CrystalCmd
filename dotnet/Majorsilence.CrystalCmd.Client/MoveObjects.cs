namespace Majorsilence.CrystalCmd.Client
{
    public class MoveObjects
    {
        public string ObjectName { get; set; }
        public int Move { get; set; }
        public MoveType Type { get; set; }
        public MovePosition Pos { get; set; }
    }
}
