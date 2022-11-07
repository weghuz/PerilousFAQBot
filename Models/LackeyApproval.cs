using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FAQBot.Models
{
    public class LackeyApproval
    {
        [Key]
        public int Id { get; set; }
        public int ApplicationId { get; set; }
        [ForeignKey(name: "ApplicationId")]
        public LackeyApplication Application { get; set; }
        public DateTime TimeStamp { get; set; }
        public ulong ApproverId { get; set; }
    }
}