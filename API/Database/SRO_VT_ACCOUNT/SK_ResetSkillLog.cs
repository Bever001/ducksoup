using System.ComponentModel.DataAnnotations;

namespace API.Database.SRO_VT_ACCOUNT
{
    public partial class SK_ResetSkillLog
    {
        public int id { get; set; }

        public int? JID { get; set; }

        [StringLength(20)]
        public string struserid { get; set; }

        [StringLength(20)]
        public string charname { get; set; }

        [StringLength(20)]
        public string SkillDown { get; set; }

        [StringLength(50)]
        public string NewSkill { get; set; }

        [StringLength(20)]
        public string SilkDown { get; set; }

        [StringLength(20)]
        public string server { get; set; }

        public DateTime? TimeReset { get; set; }
    }
}
