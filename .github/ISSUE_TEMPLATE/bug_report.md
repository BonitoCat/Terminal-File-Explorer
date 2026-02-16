---
name: Bug report
about: Create a report to help us improve
title: ''
labels: bug
assignees: BonitoCat

---

body:
  - type: dropdown
    id: priority
    attributes:
      label: Priority
      description: How important is this issue?
      options:
        - Low
        - Medium
        - High
    validations:
      required: true

  - type: textarea
    id: description
    attributes:
      label: Describe the bug
      description: A clear and concise description of what the bug is.
    validations:
      required: true

  - type: textarea
    id: reproduce
    attributes:
      label: To Reproduce
      description: Steps to reproduce the behavior
      placeholder: |
        1. Go to '...'
        2. Click on '...'
        3. Scroll down to '...'
        4. See error
    validations:
      required: true

  - type: textarea
    id: expected
    attributes:
      label: Expected behavior
      description: What did you expect to happen?

  - type: textarea
    id: screenshots
    attributes:
      label: Screenshots
      description: Add screenshots if applicable.

  - type: input
    id: os
    attributes:
      label: Operating System
      placeholder: e.g. Windows 10, Linux Mint, ...

  - type: textarea
    id: additional
    attributes:
      label: Additional context
      description: Add any other context about the problem here.

