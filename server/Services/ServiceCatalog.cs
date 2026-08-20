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

        // Police — citizen-facing interactions.
        new("crime-report", "Police", "Report a Crime (Non-Emergency)", "File a guided online incident report with eligibility screening, structured incident details, temporary report number, review status, supplements, and printable confirmation.", "shield", true, "Online report"),
        new("crime-tip", "Police", "Provide a Tip", "Share information about a serious crime using an anonymous-by-default Crime Stoppers-style intake.", "tip", true, "Anonymous by default"),
        new("police-non-emergency", "Police", "Non-Emergency Police Assistance", "Request help with a non-emergency police matter or ask to be connected with non-emergency police services.", "phone", true, "311 / NEPS"),
        new("police-report-copy", "Police", "Request an Incident Report", "Request or locate a copy of a previously filed police incident report.", "document", true, "Records"),
        new("crash-report-copy", "Police", "Request a Crash Report", "Request or locate a motor-vehicle crash report when you are an authorized party.", "car", true, "Records"),
        new("police-records", "Police", "Police Public Records Request", "Request non-routine CMPD public records through a guided records-request intake.", "records", true, "Public records"),
        new("911-recording", "Police", "Request 911 / Calls-for-Service Records", "Request a police 911 recording or calls-for-service record. This does not request an officer response.", "audio", true, "Records"),
        new("body-camera", "Police", "Request Body-Worn Camera Footage", "Start a request for body-worn camera or related police video records, subject to applicable law and agency review.", "camera", true, "Records"),
        new("officer-complaint", "Police", "Submit an Officer / Employee Complaint", "Submit feedback about alleged misconduct or service concerns, with an option to provide contact information for follow-up.", "feedback", true, "Feedback"),
        new("officer-commendation", "Police", "Commend a Police Employee", "Recognize a CMPD employee for positive service or conduct.", "star", true, "Feedback"),
        new("alarm-permit", "Police", "Alarm Permit / False Alarm Account", "Start an alarm registration, renewal, account-help, fine, or false-alarm appeal request.", "alarm", true, "Alarm management"),
        new("traffic-complaint", "Police", "Traffic Safety Complaint", "Report recurring speeding, reckless driving, parking, or other neighborhood traffic-safety concerns.", "traffic", true, "Traffic"),
        new("extra-patrol", "Police", "Extra Patrol / Vacation Watch", "Request additional patrol attention for a residence, business, or recurring concern while you are away or after a documented issue.", "patrol", true, "Community safety"),
        new("picket-notification", "Police", "Picket / Demonstration Notification", "Provide event and organizer information for a large picket or demonstration notification. This prototype is not a permit.", "event", true, "Notification"),
        new("community-police-program", "Police", "Community Program / Officer Request", "Ask about a community-safety program, neighborhood meeting, youth program, presentation, or police participation at an event.", "community", true, "Community"),
        new("suspicious-activity", "Police", "Report Suspicious Activity", "Document non-emergency suspicious circumstances for review. Call 911 if a crime is in progress or anyone is in immediate danger.", "eye", true, "Non-emergency"),
        new("found-property", "Police", "Found / Lost Property Help", "Request guidance or document non-emergency found or lost property that does not qualify for the online crime-report flow.", "property", true, "Property"),

        // Fire — current Charlotte Fire citizen request families.
        new("fire-hazard", "Fire", "Report a Fire Hazard", "Report a non-emergency fire or life-safety hazard for Fire Prevention review. Call 911 for active fire, smoke, gas, explosion, or immediate danger.", "flame", false, "Fire Prevention"),
        new("fire-report-copy", "Fire", "Request a Fire Report", "Request a copy of a Charlotte Fire incident or fire report.", "document", false, "Records"),
        new("smoke-co-alarm", "Fire", "Request a Smoke / CO Alarm", "Request information about or installation assistance for smoke and carbon-monoxide alarms.", "alarm", false, "Home safety"),
        new("fire-inspection", "Fire", "Request a Fire Inspection", "Request a fire/life-safety inspection or ask a Fire Prevention inspection question.", "inspection", false, "Inspection"),
        new("foster-home-inspection", "Fire", "Request a Foster Home Inspection", "Request a fire-safety inspection associated with a foster-home process.", "home", false, "Inspection"),
        new("hazmat-fire-records", "Fire", "Hazmat / Fire Inspection Property Records", "Request available hazardous-material or fire-inspection property record information.", "records", false, "Property records"),
        new("system-impairment", "Fire", "Fire Protection System Impairment Notice", "Notify Fire Prevention about an impairment to a fire alarm, sprinkler, suppression, or related protection system.", "system", false, "Notification"),
        new("fire-permit", "Fire", "Fire Prevention Permit / Application", "Start a Fire Prevention permit-related request and provide facility, applicant, emergency-contact, and permit information.", "permit", false, "Permit"),
        new("fire-payment", "Fire", "Fire Prevention Payment Help", "Request help identifying or resolving a Fire Prevention payment or fee transaction.", "payment", false, "Payments"),
        new("fire-education", "Fire", "Request a Fire Truck / Education Program", "Request a fire-safety education program, apparatus visit, or community outreach appearance.", "education", false, "Community"),
        new("fire-station-tour", "Fire", "Request a Fire Station Tour", "Request a community or group tour of a Charlotte fire station.", "station", false, "Community"),
        new("fire-event-staff", "Fire", "Hire Off-Duty EMTs / Firefighters", "Request off-duty Charlotte Fire EMT or firefighter staffing for an event, subject to availability and agency approval.", "medical", false, "Event staffing"),
        new("fire-code-question", "Fire", "Fire Code / Prevention Question", "Ask a non-emergency question about fire prevention, life safety, inspections, or code-related requirements.", "question", false, "Fire Prevention")
    };
}
