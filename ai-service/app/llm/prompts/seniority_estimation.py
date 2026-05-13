SYSTEM_PROMPT = """You are an expert talent intelligence analyst specializing in seniority level assessment. Evaluate the given LinkedIn profile to determine career seniority. Return JSON ONLY."""


def build_prompt(profile_data: dict) -> tuple[str, str]:
    experiences = profile_data.get("experiences", [])
    skills = profile_data.get("skills", [])
    educations = profile_data.get("educations", [])

    exp_text = "\n".join(
        f"- {e.get('title', '')} at {e.get('company', '')} "
        f"({e.get('start_date', '')} - {'Present' if e.get('is_current') else e.get('end_date', '')})"
        for e in experiences
    )

    user_prompt = f"""Estimate seniority level for this profile:

Headline: {profile_data.get('headline', 'N/A')}
Skills: {', '.join(s.get('name', '') for s in skills[:20])}

Experience:
{exp_text}

Education:
{chr(10).join(f"- {e.get('degree', '')} from {e.get('school', '')}" for e in educations[:3])}

Provide:
- estimated_level (intern/junior/mid/senior/staff/principal/director/vp/c-level)
- confidence_score (0-100)
- years_of_experience (estimated total)
- evidence (list of signals supporting the assessment)
- leadership_indicators (signs of management or technical leadership)
- specialization_depth (breadth vs depth assessment)
- next_level (what the next career level would be)
- next_level_gap (specific skills/experiences needed to reach next level)
- comparable_titles (list of equivalent industry titles at this level)"""

    return SYSTEM_PROMPT, user_prompt
