#!/bin/bash
# ==============================================================================
# APE Database Automated Import Utility (Bash)
# Imports all 20 JSON collection files into Local MongoDB or MongoDB Atlas
# ==============================================================================

URI="${1:-mongodb://localhost:27017/APE_DB}"
DATABASE="APE_DB"

echo "======================================================================"
echo " 🗄️  APE DATABASE AUTOMATED IMPORT UTILITY"
echo " Target Database URI: $URI"
echo "======================================================================"

declare -A collections=(
    ["APE_DB.SystemSettings.json"]="system_settings"
    ["APE_DB.AIAgents.json"]="ai_agents"
    ["APE_DB.AI_Rule_Artifacts.json"]="ai_rule_artifacts"
    ["APE_DB.Courses.json"]="courses"
    ["APE_DB.Users.json"]="users"
    ["APE_DB.TopupPackages.json"]="topup_packages"
    ["APE_DB.Documents.json"]="documents"
    ["APE_DB.AIExtractionDrafts.json"]="ai_extraction_drafts"
    ["APE_DB.KnowledgeChunks.json"]="knowledge_chunks"
    ["APE_DB.AIContextPacks.json"]="ai_context_packs"
    ["APE_DB.FEQuestions.json"]="fe_questions"
    ["APE_DB.PEQuestions.json"]="pe_questions"
    ["APE_DB.Exams.json"]="exams"
    ["APE_DB.PracticeSessions.json"]="practice_sessions"
    ["APE_DB.FESubmissions.json"]="fe_submissions"
    ["APE_DB.PESubmissions.json"]="pe_submissions"
    ["APE_DB.AIMentorFeedbacks.json"]="ai_mentor_feedbacks"
    ["APE_DB.Payments.json"]="payments"
    ["APE_DB.AI_VND_Billing_Transactions.json"]="ai_vnd_billing_transactions"
    ["APE_DB.AIUsageLogs.json"]="ai_usage_logs"
)

success=0
total=${#collections[@]}

for file in "${!collections[@]}"; do
    col="${collections[$file]}"
    if [ -f "$file" ]; then
        echo -n "⏳ Importing $col from $file ... "
        mongoimport --uri "$URI" --collection "$col" --file "$file" --jsonArray --mode=upsert > /dev/null 2>&1
        if [ $? -eq 0 ]; then
            echo "[SUCCESS ✅]"
            ((success++))
        else
            echo "[FAILED ❌]"
        fi
    else
        echo "⚠️ File not found: $file"
    fi
done

echo "======================================================================"
echo "🎉 Import Completed: $success / $total Collections successfully imported!"
echo "======================================================================"
