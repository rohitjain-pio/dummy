namespace SchoolSync.Api.Errors;

// 08-error-catalogue.md: every response body shape is { "code", "message" } — the code is the
// stable ERR- identifier, the message is the catalogue's own user-facing wording verbatim
// (never re-worded per endpoint).
public record ErrorResponse(string Code, string Message);

public static class ErrCatalogue
{
    public static ErrorResponse Err101 => new("ERR-101", "An institution with this name, email and postal code is already registered.");
    public static ErrorResponse Err102 => new("ERR-102", "You don't have permission to do this.");
    public static ErrorResponse Err201 => new("ERR-201", "Your password doesn't meet the minimum requirements.");
    public static ErrorResponse Err202 => new("ERR-202", "Your username or password is incorrect.");
    public static ErrorResponse Err203 => new("ERR-203", "That username or email is already registered.");
    public static ErrorResponse Err204 => new("ERR-204", "Your session has expired. Please sign in again.");
    public static ErrorResponse Err205 => new("ERR-205", "You don't have permission to do this.");
    // CT-801 / U38. ERR-206 is deliberately the single answer to unknown, revoked, consumed,
    // malformed AND empty codes — one wording, so the endpoint is neither a code-format oracle nor
    // a branch-selector oracle (VAL-027, N13). ERR-207 is the one distinguishable failure: a
    // holder of a genuine code needs to be told to ask for a new one.
    public static ErrorResponse Err206 => new("ERR-206", "This sign-up code isn't valid.");
    public static ErrorResponse Err207 => new("ERR-207", "This sign-up code has expired. Ask your school for a new one.");
    public static ErrorResponse Err208 => new("ERR-208", "This sign-up code has already been used and can't be changed.");
    public static ErrorResponse Err301 => new("ERR-301", "That register number or roll number is already in use.");
    public static ErrorResponse Err303 => new("ERR-303", "This class already has a class teacher, or this teacher is already a class teacher elsewhere.");
    public static ErrorResponse Err304 => new("ERR-304", "Reassign or release this teacher's class/subject assignments before removing them.");
    public static ErrorResponse Err305 => new("ERR-305", "A class with this name and section already exists.");
    public static ErrorResponse Err306 => new("ERR-306", "This class still has students placed in it.");
    public static ErrorResponse Err307 => new("ERR-307", "This code has already been used and can't be changed.");
    public static ErrorResponse Err308 => new("ERR-308", "This code has expired. Ask a staff member for a new one.");
    public static ErrorResponse Err309 => new("ERR-309", "This code isn't valid for your school.");
    public static ErrorResponse Err310 => new("ERR-310", "This code isn't valid.");
    public static ErrorResponse Err401 => new("ERR-401", "This attendance record is locked and can no longer be changed.");
    public static ErrorResponse Err402 => new("ERR-402", "This correction needs approval from school leadership before it takes effect.");
    public static ErrorResponse Err403 => new("ERR-403", "Attendance can't be recorded for a future date.");
    public static ErrorResponse Err501 => new("ERR-501", "Only school leadership can withdraw an announcement.");
    public static ErrorResponse Err502 => new("ERR-502", "That passcode isn't correct.");
    public static ErrorResponse Err503 => new("ERR-503", "Only the room's creator or school leadership can remove this room.");
    public static ErrorResponse Err504 => new("ERR-504", "Messages can't be longer than 4,000 characters.");
    public static ErrorResponse Err505 => new("ERR-505", "You need to join this room before joining its meeting.");
    public static ErrorResponse Err903 => new("ERR-903", "Something went wrong on our end. Please try again, and contact support if it continues.");
    public static ErrorResponse Err701 => new("ERR-701", "This teacher is already scheduled for another class at this time.");
    public static ErrorResponse Err702 => new("ERR-702", "This class already has a period scheduled at this time.");
    public static ErrorResponse Err703 => new("ERR-703", "This room is already booked at this time.");
    public static ErrorResponse Err801 => new("ERR-801", "This action would exceed your plan's limit. Upgrade your plan to continue.");
    public static ErrorResponse Err802 => new("ERR-802", "This feature isn't available on your current plan.");
    public static ErrorResponse Err901 => new("ERR-901", "Something about this request wasn't right — please check and try again.");
    public static ErrorResponse Err902 => new("ERR-902", "We couldn't find what you're looking for.");
}
