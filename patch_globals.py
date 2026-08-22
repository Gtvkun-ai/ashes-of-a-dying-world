import re

with open('project.godot', 'r') as f:
    txt = f.read()

new_globals = """[shader_globals]
env_daylight={"type": "float", "value": 1.0}
env_night={"type": "float", "value": 0.0}
env_sun_direction={"type": "vec2", "value": "Vector2(0, 1)"}
env_sun_color={"type": "color", "value": "Color(1, 1, 1, 1)"}
env_ambient_color={"type": "color", "value": "Color(1, 1, 1, 1)"}
env_wind={"type": "float", "value": 0.0}
env_rain={"type": "float", "value": 0.0}
env_wetness={"type": "float", "value": 0.0}
env_time={"type": "float", "value": 0.0}
"""

if "[shader_globals]" in txt:
    txt = re.sub(r'\[shader_globals\].*', new_globals, txt, flags=re.DOTALL)
else:
    txt += "\n" + new_globals

with open('project.godot', 'w') as f:
    f.write(txt)
