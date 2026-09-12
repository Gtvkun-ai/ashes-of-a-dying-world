from pathlib import Path
import unittest


REPO_ROOT = Path(__file__).resolve().parents[2]


class SkillIconContractTests(unittest.TestCase):
    def test_active_skills_use_character_specific_icon_art(self):
        expected_icons = {
            "data/combat/skills/hikaru_focus.tres":
                "res://assets/graphics/characters/player/icon_skill/hikaru_focus.png",
            "data/combat/skills/hyou_ice_bolt.tres":
                "res://assets/graphics/characters/hyou/icon_skill/hyou_ice_bolt_v2.png",
            "data/combat/skills/hyou_frost_ward.tres":
                "res://assets/graphics/characters/hyou/icon_skill/hyou_frost_ward_v2.png",
        }

        for resource_path, icon_path in expected_icons.items():
            with self.subTest(resource=resource_path):
                contents = (REPO_ROOT / resource_path).read_text(encoding="utf-8")
                self.assertIn(icon_path, contents)
                self.assertTrue((REPO_ROOT / icon_path.removeprefix("res://")).is_file())

    def test_cooldown_hud_uses_shared_icon_resolver(self):
        contents = (
            REPO_ROOT / "scripts/UI/HUD/SkillCooldownHudService.cs"
        ).read_text(encoding="utf-8")

        self.assertIn("view.Icon.Texture = SkillIconResolver.Resolve(skill);", contents)


if __name__ == "__main__":
    unittest.main()
