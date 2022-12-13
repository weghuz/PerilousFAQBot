using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAQBot.Models
{
    public class InfoGraphic
    {
        public InfoGraphic()
        {
            Tags = new();
        }

        public InfoGraphic(string name, string link, string image, string description, List<IGTag> tags)
        {
            Name = name;
            Image = image;
            Link = link;
            Tags = tags;
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Image { get; set; }
        public string Link { get; set; }
        public DateTime TimeStamp { get; set; }
        public List<IGTag> Tags { get; set; }
    }
}
