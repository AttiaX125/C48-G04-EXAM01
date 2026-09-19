using System;

namespace ExamSystem
{
    // ===================== Helpers =====================
    public static class Input
    {
        public static int ReadInt(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out int v) && v >= min && v <= max)
                    return v;
                Console.WriteLine($"  Invalid. Enter a number between {min} and {max}.");
            }
        }

        public static string ReadText(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string s = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                Console.WriteLine("  Value cannot be empty.");
            }
        }
    }

    // ===================== Answer =====================
    public class Answer : ICloneable, IComparable<Answer>
    {
        public int AnswerId { get; set; }
        public string AnswerText { get; set; }

        public Answer() : this(0, string.Empty) { }

        public Answer(int answerId, string answerText)
        {
            AnswerId = answerId;
            AnswerText = answerText;
        }

        public object Clone() => new Answer(AnswerId, AnswerText);

        public int CompareTo(Answer other) =>
            other == null ? 1 : AnswerId.CompareTo(other.AnswerId);

        public override string ToString() => $"{AnswerId}. {AnswerText}";
    }

    // ===================== Questions =====================
    public abstract class Question : ICloneable, IComparable<Question>
    {
        public string Header { get; set; }
        public string Body { get; set; }
        public int Mark { get; set; }
        public Answer[] AnswerList { get; set; }
        public Answer RightAnswer { get; set; }

        protected Question() : this(string.Empty, string.Empty, 0) { }

        protected Question(string header, string body, int mark)
        {
            Header = header;
            Body = body;
            Mark = mark;
            AnswerList = new Answer[0];
        }

        protected Question(string header, string body, int mark, Answer[] answers, int rightAnswerId)
            : this(header, body, mark)
        {
            AnswerList = answers;
            RightAnswer = Array.Find(answers, a => a.AnswerId == rightAnswerId)
                          ?? throw new ArgumentException("Right answer id not found in answer list.");
        }

        protected Answer[] CloneAnswers()
        {
            Answer[] copy = new Answer[AnswerList.Length];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = (Answer)AnswerList[i].Clone();
            return copy;
        }

        public virtual void Display()
        {
            Console.WriteLine($"{Header}  (Mark: {Mark})");
            Console.WriteLine(Body);
            foreach (Answer a in AnswerList)
                Console.WriteLine($"   {a}");
        }

        public bool IsCorrect(int answerId) => RightAnswer.AnswerId == answerId;

        public Answer GetAnswerById(int id) => Array.Find(AnswerList, a => a.AnswerId == id);

        public abstract object Clone();

        // Questions are ordered by mark
        public int CompareTo(Question other) =>
            other == null ? 1 : Mark.CompareTo(other.Mark);

        public override string ToString() =>
            $"[{GetType().Name}] {Header} - {Body} (Mark: {Mark})";
    }

    public class TrueFalseQuestion : Question
    {
        public TrueFalseQuestion() : base() { }

        public TrueFalseQuestion(string header, string body, int mark, bool rightAnswer)
            : base(header, body, mark,
                   new[] { new Answer(1, "True"), new Answer(2, "False") },
                   rightAnswer ? 1 : 2)
        { }

        // used by Clone
        private TrueFalseQuestion(TrueFalseQuestion src)
            : base(src.Header, src.Body, src.Mark, src.CloneAnswers(), src.RightAnswer.AnswerId)
        { }

        public override object Clone() => new TrueFalseQuestion(this);
    }

    public class MCQQuestion : Question
    {
        public MCQQuestion() : base() { }

        public MCQQuestion(string header, string body, int mark, Answer[] answers, int rightAnswerId)
            : base(header, body, mark, answers, rightAnswerId)
        { }

        private MCQQuestion(MCQQuestion src)
            : base(src.Header, src.Body, src.Mark, src.CloneAnswers(), src.RightAnswer.AnswerId)
        { }

        public override object Clone() => new MCQQuestion(this);
    }

    // ===================== Exams =====================
    public abstract class Exam : ICloneable, IComparable<Exam>
    {
        public int Time { get; set; }                 // minutes
        public int NumberOfQuestions { get; set; }
        public Subject Subject { get; set; }
        public Question[] Questions { get; protected set; }
        protected int[] StudentAnswers;
        private int _count;                           // questions added so far

        protected Exam() : this(0, 0, null) { }

        protected Exam(int time, int numberOfQuestions) : this(time, numberOfQuestions, null) { }

        protected Exam(int time, int numberOfQuestions, Subject subject)
        {
            Time = time;
            NumberOfQuestions = numberOfQuestions;
            Subject = subject;
            Questions = new Question[numberOfQuestions];
            StudentAnswers = new int[numberOfQuestions];
        }

        // Each exam type decides which question types it accepts
        protected abstract bool IsAllowed(Question q);

        // Each exam type builds its questions differently
        public abstract void CreateQuestions();

        // Each exam type shows itself differently
        public abstract void ShowExam();

        public void AddQuestion(Question q)
        {
            if (!IsAllowed(q))
                throw new InvalidOperationException($"{GetType().Name} does not accept {q.GetType().Name}.");
            if (_count >= Questions.Length)
                throw new InvalidOperationException("Exam is full.");
            Questions[_count++] = q;
        }

        // ---------- shared input helpers ----------
        protected static MCQQuestion ReadMCQ(int number)
        {
            string body = Input.ReadText("  Question body: ");
            int mark = Input.ReadInt("  Mark: ", 1, 1000);
            int n = Input.ReadInt("  Number of choices (2-6): ", 2, 6);
            Answer[] answers = new Answer[n];
            for (int i = 0; i < n; i++)
                answers[i] = new Answer(i + 1, Input.ReadText($"    Choice {i + 1}: "));
            int right = Input.ReadInt($"  Right choice (1-{n}): ", 1, n);
            return new MCQQuestion($"Q{number} - MCQ", body, mark, answers, right);
        }

        protected static TrueFalseQuestion ReadTrueFalse(int number)
        {
            string body = Input.ReadText("  Question body: ");
            int mark = Input.ReadInt("  Mark: ", 1, 1000);
            int right = Input.ReadInt("  Right answer (1 = True, 2 = False): ", 1, 2);
            return new TrueFalseQuestion($"Q{number} - True/False", body, mark, right == 1);
        }

        // Ask the student every question and record the answers
        protected void TakeExam()
        {
            if (_count != Questions.Length)
                throw new InvalidOperationException("Exam is incomplete: not all questions were added.");
            Console.WriteLine($"\n===== {Subject?.SubjectName} - {GetType().Name} ({Time} min) =====\n");
            for (int i = 0; i < Questions.Length; i++)
            {
                Questions[i].Display();
                StudentAnswers[i] = Input.ReadInt($"Your answer (1-{Questions[i].AnswerList.Length}): ",
                                                  1, Questions[i].AnswerList.Length);
                Console.WriteLine();
            }
        }

        public virtual object Clone()
        {
            Exam copy = (Exam)MemberwiseClone();
            copy.Questions = new Question[Questions.Length];
            for (int i = 0; i < Questions.Length; i++)
                copy.Questions[i] = (Question)Questions[i]?.Clone();
            copy.StudentAnswers = (int[])StudentAnswers.Clone();
            return copy;
        }

        // Exams are ordered by number of questions, then by time
        public int CompareTo(Exam other)
        {
            if (other == null) return 1;
            int c = NumberOfQuestions.CompareTo(other.NumberOfQuestions);
            return c != 0 ? c : Time.CompareTo(other.Time);
        }

        public override string ToString() =>
            $"{GetType().Name}: Subject={Subject?.SubjectName}, Time={Time} min, Questions={NumberOfQuestions}";
    }

    public class FinalExam : Exam
    {
        public FinalExam() : base() { }
        public FinalExam(int time, int numberOfQuestions, Subject subject)
            : base(time, numberOfQuestions, subject) { }

        // Final: True/False and MCQ
        protected override bool IsAllowed(Question q) =>
            q is TrueFalseQuestion || q is MCQQuestion;

        public override void CreateQuestions()
        {
            for (int i = 1; i <= NumberOfQuestions; i++)
            {
                Console.WriteLine($"\nQuestion {i}:");
                int type = Input.ReadInt("  Type (1 = True/False, 2 = MCQ): ", 1, 2);
                AddQuestion(type == 1 ? (Question)ReadTrueFalse(i) : ReadMCQ(i));
            }
        }

        // Final: shows questions, answers and grade
        public override void ShowExam()
        {
            TakeExam();

            Console.WriteLine("===== Final Exam Result =====");
            int grade = 0, total = 0;
            for (int i = 0; i < Questions.Length; i++)
            {
                Question q = Questions[i];
                total += q.Mark;
                bool ok = q.IsCorrect(StudentAnswers[i]);
                if (ok) grade += q.Mark;

                Console.WriteLine($"{q.Header}: {q.Body}");
                foreach (Answer a in q.AnswerList) Console.WriteLine($"   {a}");
                Console.WriteLine($"   Your answer : {q.GetAnswerById(StudentAnswers[i])}  -> {(ok ? "Correct" : "Wrong")}");
                Console.WriteLine();
            }
            Console.WriteLine($"Grade: {grade} / {total}");
        }
    }

    public class PracticalExam : Exam
    {
        public PracticalExam() : base() { }
        public PracticalExam(int time, int numberOfQuestions, Subject subject)
            : base(time, numberOfQuestions, subject) { }

        // Practical: MCQ only
        protected override bool IsAllowed(Question q) => q is MCQQuestion;

        public override void CreateQuestions()
        {
            for (int i = 1; i <= NumberOfQuestions; i++)
            {
                Console.WriteLine($"\nQuestion {i} (MCQ):");
                AddQuestion(ReadMCQ(i));
            }
        }

        // Practical: shows the right answer after finishing the exam
        public override void ShowExam()
        {
            TakeExam();

            Console.WriteLine("===== Practical Exam - Right Answers =====");
            foreach (Question q in Questions)
            {
                Console.WriteLine($"{q.Header}: {q.Body}");
                Console.WriteLine($"   Right answer: {q.RightAnswer}");
            }
        }
    }

    // ===================== Subject =====================
    public class Subject : ICloneable, IComparable<Subject>
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; }
        public Exam Exam { get; set; }

        public Subject() : this(0, string.Empty) { }

        public Subject(int subjectId, string subjectName)
        {
            SubjectId = subjectId;
            SubjectName = subjectName;
        }

        // Creates the exam of this subject (type chosen by the user)
        public void CreateExam()
        {
            int type = Input.ReadInt("Exam type (1 = Final, 2 = Practical): ", 1, 2);
            int time = Input.ReadInt("Exam time (minutes): ", 1, 600);
            int n = Input.ReadInt("Number of questions: ", 1, 100);

            Exam = type == 1
                ? (Exam)new FinalExam(time, n, this)
                : new PracticalExam(time, n, this);

            Exam.CreateQuestions();
        }

        public object Clone()
        {
            Subject copy = new Subject(SubjectId, SubjectName);
            if (Exam != null)
            {
                copy.Exam = (Exam)Exam.Clone();
                copy.Exam.Subject = copy;
            }
            return copy;
        }

        public int CompareTo(Subject other) =>
            other == null ? 1 : SubjectId.CompareTo(other.SubjectId);

        public override string ToString() => $"Subject {SubjectId}: {SubjectName}";
    }

    // ===================== Main =====================
    internal class Program
    {
        static void Main()
        {
            Subject subject = new Subject(
                Input.ReadInt("Subject Id: ", 1, int.MaxValue),
                Input.ReadText("Subject Name: "));

            subject.CreateExam();

            Console.WriteLine($"\n{subject}");
            Console.WriteLine(subject.Exam);

            subject.Exam.ShowExam();
        }
    }
}