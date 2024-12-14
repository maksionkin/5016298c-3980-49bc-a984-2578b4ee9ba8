# Design Decisions
## Incremental Counter vs Random Short URL
- Random can slow down due collisions (Birthday paradox) as more and more short urls are created.
- It is believed that manual addtons are rare and mostly "beautiful" are added like "Adroit" thus the chance of collision with counter based one is low.
- In order to counter based look random a FMV cache is used
	- more cryptographycally strong hash can be used but performance will degrade
	- alphabet randomzaton can help without a perf hit but can be discovered