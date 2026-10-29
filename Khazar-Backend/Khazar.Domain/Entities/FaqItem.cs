using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

/// <summary>A question and answer on the FAQ page (/tez-tez-verilen-suallar).</summary>
public class FaqItem : BaseEntity
{
    public const int MaxQuestionLength = 300;
    public const int MaxAnswerLength = 3000;

    public string Question { get; private set; } = default!;
    public string Answer { get; private set; } = default!;

    /// <summary>Position on the FAQ page; lower comes first.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Unpublished questions are visible in the admin panel only.</summary>
    public bool IsPublished { get; private set; }

    // Required by ORMs
    private FaqItem() { }

    public FaqItem(string question, string answer, int sortOrder, bool isPublished)
    {
        SetDetails(question, answer, sortOrder, isPublished);
    }

    public void UpdateDetails(string question, string answer, int sortOrder, bool isPublished)
    {
        SetDetails(question, answer, sortOrder, isPublished);
        MarkUpdated();
    }

    private void SetDetails(string question, string answer, int sortOrder, bool isPublished)
    {
        Question = Guard.MaxLength(Guard.NotEmpty(question, nameof(Question)), MaxQuestionLength, nameof(Question));
        Answer = Guard.MaxLength(Guard.NotEmpty(answer, nameof(Answer)), MaxAnswerLength, nameof(Answer));
        SortOrder = Guard.NotNegative(sortOrder, nameof(SortOrder));
        IsPublished = isPublished;
    }
}
