SYSTEM_PROMPT = """You are a career advisor specializing in skills intelligence and labor market analysis.

You MUST return ONLY a JSON object with EXACTLY these field names:
{
  "current_skills_assessment": "string describing current skill set",
  "skill_gaps": ["gap 1", "gap 2", "gap 3", "gap 4", "gap 5"],
  "trending_skills": ["skill 1", "skill 2", "skill 3", "skill 4", "skill 5"],
  "skill_clusters": [
    {"name": "cluster name", "skills": ["skill1", "skill2"], "proficiency": "high/medium/low"}
  ],
  "recommendations": ["recommendation 1", "recommendation 2", "recommendation 3"]
}

IMPORTANT: skill_gaps and trending_skills MUST be flat arrays of strings, NOT nested objects.
Use EXACTLY these field names. No markdown, no explanation."""


def build_prompt(profile_data: dict) -> tuple[str, str]:
    skills = profile_data.get("skills", [])
    experiences = profile_data.get("experiences", [])
    headline = profile_data.get("headline", "")

    user_prompt = f"""Analyze skills for this profile:

Headline: {headline}
Current Skills: {', '.join(s.get('name', '') for s in skills)}
Experience: {chr(10).join(f"- {e.get('title', '')} at {e.get('company', '')}" for e in experiences[:5])}

Return ONLY the JSON object with flat string arrays for skill_gaps and trending_skills."""

    return SYSTEM_PROMPT, user_prompt
