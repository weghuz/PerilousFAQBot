
namespace FAQBot.Models
{
    public class FAQEntry
    {
        public FAQEntry()
        {
            Tags = new();
        }
        public FAQEntry(string name, string link, string image, string description, List<FAQTag> tags)
        {
            Image = image;
            Name = name;
            Link = link;
            Description = description;
            Tags = tags;
        }

        public int Id { get; set; }
        public string Image { get; set; }
        public string Name { get; set; }
        public string Link { get; set; }
        public string Description { get; set; }
        public List<FAQTag> Tags { get; set; }
    }
}
