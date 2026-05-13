SYSTEM_PROMPT = """You are an expert ATS (Applicant Tracking System) optimization specialist.

You MUST return ONLY a JSON object with EXACTLY these field names:
{
  "overall_ats_score": <integer 0-100>,
  "keyword_density": {"score": 0.8, "top_keywords": ["keyword1", "keyword2"]},
  "headline_analysis": {"current_issues": "description", "suggested_rewrite": "new headline"},
  "about_analysis": {"keyword_gaps": ["gap1", "gap2"], "suggestions": ["suggestion1"]},
  "experience_improvements": [{"role": "Job Title", "original": "old text", "improved": "new text"}],
  "missing_keywords": ["keyword1", "keyword2", "keyword3"],
  "formatting_issues": ["issue1", "issue2"],
  "quick_wins": [
    {"action": "specific action to take", "impact": "high"},
    {"action": "specific action to take", "impact": "medium"},
    {"action": "specific action to take", "impact": "high"},
    {"action": "specific action to take", "impact": "medium"},
    {"action": "specific action to take", "impact": "low"}
  ]
}

IMPORTANT: quick_wins items MUST have "action" (string describing what to do) and "impact" (string: "high", "medium", or "low"). No markdown, no explanation."""


def build_prompt(profile_data: dict) -> tuple[str, str]:
    experiences = profile_data.get("experiences", [])
    skills = profile_data.get("skills", [])

    exp_text = "\n".join(
        f"- {e.get('title', '')} at {e.get('company', '')}: {e.get('description', '')[:200]}"
        for e in experiences[:5]
    )

    user_prompt = f"""Analyze this LinkedIn profile for ATS optimization:

Headline: {profile_data.get('headline', 'N/A')}
About: {(profile_data.get('about') or '')[:400]}
Skills: {', '.join(s.get('name', '') for s in skills[:25])}

Experience:
{exp_text}

Return ONLY the JSON object with all required fields. Use "action" and "impact" (high/medium/low) for quick_wins."""

    return SYSTEM_PROMPT, user_prompt
