using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace FAQBot.Models
{
    public class IGTag
    {
        public IGTag()
        {

        }
        public IGTag(int id, string tag)
        {
            Id = id;
            Tag = tag;
        }
        [Key]
        public int Id { get; set; }
        public int InfoGraphicId { get; set; }
        [ForeignKey("InfoGraphicId")]
        public InfoGraphic InfoGraphic { get; set; }
        public string Tag { get; set; }
    }
}