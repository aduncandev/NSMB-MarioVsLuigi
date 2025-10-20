using Quantum.Task;

namespace Quantum {
    public unsafe abstract class SystemMainThreadEntity<E> : SystemBase where E : unmanaged, IComponent {

        private TaskDelegateHandle updateTaskHandle;

        public override void OnInit(Frame f) {
            base.OnInit(f);
        }

        protected override TaskHandle Schedule(Frame f, TaskHandle taskHandle) {
            try {
                // Temporary, currently bugged.
                if (f.ComponentCount<E>() <= 0) {
                    return taskHandle;
                }
            } catch {
                return taskHandle;
            }
            if (!updateTaskHandle.IsValid) {
                f.Context.TaskContext.RegisterDelegate(UpdateTask, $"{GetType().Name}.UpdateTask", ref updateTaskHandle);
            }
            return f.Context.TaskContext.AddMainThreadTask(updateTaskHandle, null, taskHandle);
        }

        public void UpdateTask(FrameThreadSafe f, int start, int count, void* arg) {
            Update((Frame) f);
        }

        public abstract void Update(Frame f);

    }
}