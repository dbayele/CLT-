using CltPlusPlus.Api.Models;

namespace CltPlusPlus.Api.Services;

public static class ServiceCatalog
{
    public static readonly IReadOnlyList<ServiceDefinition> All = new List<ServiceDefinition>
    {
        new("pothole", "Streets & Transportation", "Pothole or Road Damage", "Report a pothole, pavement failure, or other roadway surface issue.", "road"),
        new("traffic-signal", "Streets & Transportation", "Traffic Signal Issue", "Report a signal that is dark, flashing, damaged, or operating unexpectedly.", "signal"),
        new("streetlight", "Streets & Transportation", "Streetlight Out", "Report a streetlight that is out, cycling, damaged, or obstructed.", "light"),
        new("missed-collection", "Solid Waste", "Missed Collection", "Report a missed garbage, recycling, yard waste, or bulky-item collection.", "trash"),
        new("illegal-dumping", "Neighborhoods", "Illegal Dumping", "Report dumped material, debris, or abandoned waste in a public area.", "dump"),
        new("graffiti", "Neighborhoods", "Graffiti", "Report graffiti on public property or infrastructure.", "spray"),
        new("tree-limb", "Trees & Environment", "Tree or Limb Concern", "Report a hazardous, fallen, or obstructing tree or limb affecting public space.", "tree"),
        new("water-leak", "Water", "Water Leak", "Report a suspected public water leak or water flowing from a utility area.", "water"),
        new("animal-concern", "Animals", "Animal Concern", "Request help with a stray animal, nuisance concern, or non-emergency animal issue.", "paw"),
        new("crime-report", "Police", "Report a Crime (Non-Emergency)", "Start a guided report for a non-emergency crime. Emergencies and crimes in progress must go to 911.", "shield", true, "Non-Emergency"),
        new("crime-tip", "Police", "Provide a Tip", "Share information about a serious crime using an anonymous-by-default Crime Stoppers-style intake.", "tip", true, "Anonymous by default")
    };
}
