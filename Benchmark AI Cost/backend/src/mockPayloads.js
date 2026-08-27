const repeatWords = (word, count) => Array.from({ length: count }, () => word).join(" ");

const itSyllabusBlock = `
This syllabus introduces core software engineering concepts including algorithms, data structures,
computer networks, web development, object-oriented programming, databases, and cloud deployment.
Students practice Java and Node.js coding exercises, build REST APIs, and analyze time complexity.
`;

export const mockPayloads = {
  AI_Gatekeeper: {
    prompt: `You are an academic classifier. Determine whether the syllabus is related to Information Technology. Answer only JSON with keys {isIT:boolean, reason:string}.\n\nSyllabus:\n${itSyllabusBlock}\n${repeatWords("software systems database coding architecture", 130)}`,
  },
  AI_Vision_Parse: {
    prompt: "Extract all text from this document image and return Markdown.",
    base64Image:
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wm6v9sAAAAASUVORK5CYII=",
  },
  AI_Embedding: {
    inputText: `${itSyllabusBlock} ${repeatWords("learning objective programming practice", 165)}`,
  },
  AI_Question_Gen: {
    prompt: `Using the following context, generate exactly 5 multiple-choice questions in valid JSON array format with fields: question, options, answer, explanation.\n\nContext:\n${repeatWords("Distributed systems consistency CAP theorem microservices observability", 285)}`,
  },
  AI_Review_Agent: {
    prompt: `You are an AI reviewer. Verify the logic and factual accuracy of these generated questions against the context. Return JSON with keys {overallScore:number, issues:string[], suggestions:string[]}.\n\nContext:\n${repeatWords("Operating systems process scheduling memory management concurrency", 170)}\n\nQuestions JSON:\n${JSON.stringify(
      [
        { question: "What does CAP stand for?", options: ["Consistency, Availability, Partition tolerance", "Cache, API, Protocol"], answer: "Consistency, Availability, Partition tolerance" },
      ],
      null,
      2
    )}`,
  },
  AI_Code_Mentor: {
    prompt: `Analyze the following Java code and test case failures. Explain the bug and provide time complexity.\n\nCode:\npublic int binarySearch(int[] a, int target){ int l=0,r=a.length-1; while(l<r){ int m=(l+r)/2; if(a[m]==target) return m; if(a[m]<target) l=m; else r=m-1; } return -1; }\n\nTest cases failing:\n1) target at last index not found\n2) infinite loop for some two-element arrays`,
  },
  AI_Summary_Analyzer: {
    prompt: `Given the student practice sessions JSON, provide concise study recommendations and risk flags.\n\nData:\n${JSON.stringify(
      Array.from({ length: 50 }, (_, i) => ({
        studentId: `S${1000 + i}`,
        avgScore: 40 + (i % 45),
        weakTopic: i % 2 === 0 ? "Algorithms" : "Databases",
        sessionsThisWeek: (i % 6) + 1,
      }))
    )}`,
  },
};
