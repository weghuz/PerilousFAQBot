using System.ComponentModel.DataAnnotations;

namespace FAQBot.Models
{
    public class LackeyApplication
    {
        [Key]
        public int Id { get; set; }
        public ulong ApplicantId { get; set; }
        public DateTime ApplicationTime { get; set; }
        public List<LackeyApproval> Approvals { get; set; }

    }
}
