using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAQBot.Models
{
    public class FAQTag
    {
        public FAQTag()
        {

        }
        public FAQTag(int id, string tag)
        {
            Id = id;
            Tag = tag;
        }
        [Key]
        public int Id { get; set; }
        public int FAQEntryId { get; set; }
        [ForeignKey("FAQEntryId")]
        public FAQEntry FAQEntry { get; set; }

        public string Tag { get; set; }
    }
}
