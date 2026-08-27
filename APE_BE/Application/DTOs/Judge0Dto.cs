namespace Application.DTOs;

public class Judge0ResultDto
{
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public string? CompileOutput { get; set; }
    public double? Time { get; set; }
    public int? Memory { get; set; }
}
