"""Keep phone reviews focused on the two most useful available movements."""

def review_clip_labels(labels):
    available = set(labels)
    priorities = [
        ('Walk', ['Run', 'Swim', 'Swim Horizontal', 'Flap', 'Flight', 'Attack', 'Idle']),
        ('Slither', ['Side winding', 'Idle']),
        ('Flap', ['Glide', 'Idle']),
        ('Flight', ['Perch', 'Idle']),
        ('Swim Horizontal', ['Swim Vertical', 'Idle']),
        ('Swim', ['Escape pulse', 'Idle']),
        ('Swim Vertical', ['Idle']),
        ('Flipper stroke transfer', ['Glide transfer', 'Idle']),
        ('Side winding', ['Idle']),
        ('Run', ['Jump', 'Idle']),
    ]
    for primary, alternatives in priorities:
        if primary in available:
            secondary = next((label for label in alternatives if label in available), None)
            if secondary:
                return [primary, secondary]
    raise ValueError(f'Choose two main review clips for {sorted(available)}')
