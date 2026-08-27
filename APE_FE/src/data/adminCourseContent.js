import { ROUTES } from "../lib/routes";

function markActive(items, activeHref) {
  return items.map((item) => ({
    ...item,
    active: Array.isArray(item.activeRoutes) ? item.activeRoutes.includes(activeHref) : item.href === activeHref
  }));
}

export function getAdminNavSections(activeHref) {
  return [
    {
      label: "Overview",
      items: markActive([
        {
          label: "Dashboard",
          icon: "grid",
          href: ROUTES.adminDashboard
        }
      ], activeHref)
    },
    {
      label: "Management",
      items: markActive([
        {
          label: "User Registry",
          icon: "user",
          href: ROUTES.userManagement
        },
        {
          label: "Course Management",
          icon: "cap",
          href: ROUTES.courseManagement
        },
        {
          label: "Question",
          icon: "edit",
          href: ROUTES.questionManagement,
          activeRoutes: [ROUTES.questionManagement, ROUTES.adminQuestionImport]
        },
        {
          label: "Practice Setup",
          icon: "spark",
          href: ROUTES.adminPracticeSetup
        },
        {
          label: "Wallet Packages",
          icon: "wallet",
          href: ROUTES.adminWalletPackages
        },
        {
          label: "Document Management",
          icon: "doc",
          href: ROUTES.documentManagement
        },
        {
          label: "Generate Questions",
          icon: "spark",
          href: ROUTES.adminGenerateQuestions
        }
      ], activeHref)
    },
    {
      label: "AI Settings",
      items: markActive([
        {
          label: "Providers",
          icon: "spark",
          href: ROUTES.adminAiProviders
        },
        {
          label: "AI Agents",
          icon: "user",
          href: ROUTES.adminAiAgents
        },
        {
          label: "Rule Artifacts",
          icon: "doc",
          href: ROUTES.adminAiRuleArtifacts
        },
        {
          label: "AI Wallet Billing",
          icon: "wallet",
          href: ROUTES.adminAiBillingConfig
        }
        // Runtime Health is not in use yet.
        // {
        //   label: "Runtime Health",
        //   icon: "grid",
        //   href: ROUTES.adminAiRuntimeHealth
        // }
      ], activeHref)
    },
    {
      label: "AI Monitoring",
      items: markActive([
        {
          label: "Usage Logs",
          icon: "filter",
          href: ROUTES.adminAiUsageLogs
        },
        // Mentor Feedbacks belongs to student-facing flows, not admin monitoring.
        // {
        //   label: "Mentor Feedbacks",
        //   icon: "edit",
        //   href: ROUTES.adminAiMentorFeedbacks
        // }
      ], activeHref)
    },
    {
      label: "AI Smoke Tests",
      items: markActive([
        {
          label: "1. Gatekeeper",
          icon: "filter",
          href: ROUTES.adminAiSmokeGatekeeper
        },
        {
          label: "2. Extracted Content",
          icon: "grid",
          href: ROUTES.adminAiSmokeExtractedContent
        },
        {
          label: "3. Embedding + AutoTagging",
          icon: "spark",
          href: ROUTES.adminAiSmokeEmbedding
        },
        {
          label: "4. Generation + Review",
          icon: "doc",
          href: ROUTES.adminAiSmokeQuestionGenerationReview
        },
        {
          label: "5. Code Mentor",
          icon: "help",
          href: ROUTES.adminAiSmokeCodeMentor
        },
        // API Probe is temporarily hidden from admin navigation.
        // {
        //   label: "Support: API Probe",
        //   icon: "search",
        //   href: ROUTES.adminAiApiProbe
        // }
        // Retrieval Plan is not in use yet.
        // {
        //   label: "Support: Retrieval Plan",
        //   icon: "search",
        //   href: ROUTES.adminAiSmokeRetrievalPlan
        // }
      ], activeHref)
    }
  ];
}

export function getAdminNavItems(activeHref) {
  return getAdminNavSections(activeHref).flatMap((section) => section.items);
}
