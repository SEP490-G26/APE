# ==============================================================================
# APE Database Automated Import Utility (PowerShell)
# Imports all 20 JSON collection files into Local MongoDB or MongoDB Atlas
# ==============================================================================

param (
    [string]$Uri = "mongodb://localhost:27017/APE_DB",
    [string]$Database = "APE_DB"
)

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " 🗄️  APE DATABASE AUTOMATED IMPORT UTILITY" -ForegroundColor Cyan
Write-Host " Target Database URI: $Uri" -ForegroundColor Yellow
Write-Host " Target Database:     $Database" -ForegroundColor Yellow
Write-Host "======================================================================" -ForegroundColor Cyan

# Collection mapping dictionary
$collectionMap = [ordered]@{
    "APE_DB.SystemSettings.json"                  = "system_settings"
    "APE_DB.AIAgents.json"                        = "ai_agents"
    "APE_DB.AI_Rule_Artifacts.json"               = "ai_rule_artifacts"
    "APE_DB.Courses.json"                         = "courses"
    "APE_DB.Users.json"                           = "users"
    "APE_DB.TopupPackages.json"                   = "topup_packages"
    "APE_DB.Documents.json"                       = "documents"
    "APE_DB.AIExtractionDrafts.json"              = "ai_extraction_drafts"
    "APE_DB.KnowledgeChunks.json"                 = "knowledge_chunks"
    "APE_DB.AIContextPacks.json"                  = "ai_context_packs"
    "APE_DB.FEQuestions.json"                     = "fe_questions"
    "APE_DB.PEQuestions.json"                     = "pe_questions"
    "APE_DB.Exams.json"                           = "exams"
    "APE_DB.PracticeSessions.json"                = "practice_sessions"
    "APE_DB.FESubmissions.json"                   = "fe_submissions"
    "APE_DB.PESubmissions.json"                   = "pe_submissions"
    "APE_DB.AIMentorFeedbacks.json"               = "ai_mentor_feedbacks"
    "APE_DB.Payments.json"                        = "payments"
    "APE_DB.AI_VND_Billing_Transactions.json"     = "ai_vnd_billing_transactions"
    "APE_DB.AIUsageLogs.json"                     = "ai_usage_logs"
}

# Check mongoimport availability
$mongoImportCmd = Get-Command "mongoimport" -ErrorAction SilentlyContinue
if (-not $mongoImportCmd) {
    Write-Host "⚠️ Warning: 'mongoimport' command not found in PATH." -ForegroundColor Yellow
    Write-Host "Please install MongoDB Database Tools or import via MongoDB Compass." -ForegroundColor Yellow
    Write-Host "Download: https://www.mongodb.com/try/download/database-tools" -ForegroundColor White
    exit 1
}

$successCount = 0
$totalCount = $collectionMap.Count

foreach ($file in $collectionMap.Keys) {
    $col = $collectionMap[$file]
    $filePath = Join-Path $PSScriptRoot $file

    if (Test-Path $filePath) {
        Write-Host "⏳ Importing $col from $file ..." -NoNewline
        try {
            $output = mongoimport --uri "$Uri" --collection "$col" --file "$filePath" --jsonArray --mode=upsert 2>&1
            Write-Host " [SUCCESS ✅]" -ForegroundColor Green
            $successCount++
        }
        catch {
            Write-Host " [FAILED ❌]" -ForegroundColor Red
            Write-Host "   Error: $_" -ForegroundColor Red
        }
    } else {
        Write-Host "⚠️ File not found: $file" -ForegroundColor DarkYellow
    }
}

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "🎉 Import Completed: $successCount / $totalCount Collections successfully imported!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Cyan
