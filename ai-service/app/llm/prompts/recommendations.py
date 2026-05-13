SYSTEM_PROMPT = """You are a strategic career coach with expertise in LinkedIn optimization, career development, and talent markets.

You MUST return ONLY a JSON object with EXACTLY these field names:
{
  "immediate_actions": [
    {"title": "action title", "description": "detailed description", "impact": "high"}
  ],
  "short_term_goals": [
    {"goal": "goal description", "steps": ["step 1", "step 2"], "success_metric": "how to measure"}
  ],
  "long_term_goals": [
    {"goal": "goal description", "milestones": ["milestone 1"], "success_metric": "how to measure"}
  ],
  "learning_path": [
    {"name": "course name", "platform": "Coursera/Udemy/etc", "reason": "why", "priority": "high"}
  ],
  "networking_strategy": "string with specific networking advice",
  "profile_quick_fixes": [
    {"fix": "what to change", "score_improvement": 5}
  ],
  "job_search_strategy": {
    "target_companies": ["company 1", "company 2"],
    "target_roles": ["role 1", "role 2"],
    "approach": "description of approach"
  },
  "personal_brand_tips": ["tip 1", "tip 2", "tip 3"]
}

Use EXACTLY these field names. No markdown, no explanation."""


def _safe_list(val: object) -> list:
    """Safely coerce dict/str/None to a flat list of strings."""
    if val is None:
        return []
    if isinstance(val, list):
        return val
    if isinstance(val, dict):
        # Flatten nested dict values into a single list
        result = []
        for v in val.values():
            if isinstance(v, list):
                result.extend(str(i) for i in v)
            else:
                result.append(str(v))
        return result
    return [str(val)]


def build_prompt(profile_data: dict, analysis_context: dict) -> tuple[str, str]:
    scoring = analysis_context.get("profile_scoring") or {}
    skills = analysis_context.get("skills_intelligence") or {}
    career = analysis_context.get("career_analysis") or {}
    ats = analysis_context.get("ats_optimization") or {}
    seniority = analysis_context.get("seniority_estimation") or {}

    skill_gaps = _safe_list(skills.get("skill_gaps"))[:5]
    trending_skills = _safe_list(skills.get("trending_skills"))[:5]
    career_risks = _safe_list(career.get("career_risks"))[:3]
    predicted_roles = _safe_list(career.get("predicted_next_roles"))
    next_role = predicted_roles[0] if predicted_roles else "N/A"

    user_prompt = f"""Generate comprehensive career recommendations for:

Profile: {profile_data.get('name', 'N/A')} — {profile_data.get('headline', 'N/A')}

Analysis Summary:
- Profile Score: {scoring.get('overall_score', 'N/A')}/100
- ATS Score: {ats.get('overall_ats_score', 'N/A')}/100
- Seniority: {seniority.get('estimated_level', 'N/A')} ({seniority.get('years_of_experience', 'N/A')} years)
- Career Trajectory: {career.get('career_trajectory', 'N/A')}
- Top Skill Gaps: {', '.join(skill_gaps) if skill_gaps else 'None identified'}
- Trending Skills Missing: {', '.join(trending_skills) if trending_skills else 'None identified'}
- Next Predicted Role: {next_role}
- Career Risks: {', '.join(career_risks) if career_risks else 'None identified'}

Return ONLY the JSON object with all required fields."""

    return SYSTEM_PROMPT, user_prompt
