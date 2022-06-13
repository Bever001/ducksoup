using System.ComponentModel.DataAnnotations;

namespace API.Database.SRO_VT_SHARD
{
    public partial class Tab_RefHive
    {
        [Key]
        public int dwHiveID { get; set; }

        public byte? btKeepMonsterCountType { get; set; }

        public int? dwOverwriteMaxTotalCount { get; set; }

        public float? fMonsterCountPerPC { get; set; }

        public int? dwSpawnSpeedIncreaseRate { get; set; }

        public int? dwMaxIncreaseRate { get; set; }

        public byte? btFlag { get; set; }

        public short? GameWorldID { get; set; }

        public short? HatchObjType { get; set; }

        [StringLength(128)]
        public string szDescString128 { get; set; }
    }
}
